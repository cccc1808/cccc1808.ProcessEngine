using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components.Outbox;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.Services
{
    /// <summary>
    /// Outbox process -> queue.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class OutboxRangeProcessHandler<TId>
        : BaseRangeProcessHandler<TId>
    {
        private readonly IQueueProviderFactory _queueProviderFactory;
        private readonly IInboxOutboxSetter _inboxOutboxSetter;

        public OutboxRangeProcessHandler(
            IProcessRepository<TId> repository,
            ITriggerRepository<TId> triggerRepository,
            IProcessSetter setter,
            IQueueProviderFactory queueProviderFactory,
            IInboxOutboxSetter inboxOutboxSetter)
            : base(
                  repository,
                  triggerRepository,
                  setter)
        {
            _queueProviderFactory = queueProviderFactory;
            _inboxOutboxSetter = inboxOutboxSetter;
        }

        public override ExecuteStepByStepGroupMiddleware<TId>.OptionsDto Options => throw new NotImplementedException();

        public override async ValueTask StepRangeAsync(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group,
            CancellationToken cancellationToken)
        {
            group = _inboxOutboxSetter.PrepareOutboxGroup(group);

            var context = group.Group
                .Select(e => (
                    Process: e.Value, 
                    Outbox: e.Value.GetComponent<IOutboxComponent<TId>>())
                    )
                .ToDictionary(
                    e => e.Process.Id,
                    e => e);

            // Группируем по очередям.
            var groupByQueue = context.Values
                .GroupBy(e => e.Outbox.Queue)
                .ToArray();
            
            foreach (var elem in groupByQueue)
            {
                var queueBatch = elem
                    .SelectMany(e1 => e1.Outbox.Messages
                        .Select(e2 => (Process: e1.Process, Outbox: e1.Outbox, Message: e2))
                        )
                    .OrderByDescending(e => e.Message.Priority)
                    .ThenBy(e => e.Message.OrderId)
                    .Select(e => (
                        Message: e,
                        producerMessage: BuildMessage(e.Process, e.Outbox, e.Message)
                        ))
                    .ToArray();

                var producer = await _queueProviderFactory.GetProducerAsync(elem.Key, cancellationToken);
                try
                {
                    await producer.ProduceBatchAsync(
                        queueBatch.Select(e => e.producerMessage).ToArray(),
                        cancellationToken);

                    foreach (var elem2 in queueBatch)
                    {
                        _inboxOutboxSetter.OutboxMessageProcessed(
                            elem2.Message.Process,
                            elem2.Message.Outbox,
                            elem2.Message.Message);
                    }
                }
                catch (Exception ex)
                {
                    if (OperationCancelHelper.IsCancelException(ex, cancellationToken))
                    {
                        throw;
                    }

                    foreach (var elem2 in elem)
                    {
                        var errorResult = _processSetter.SetError(elem2.Process, ex, allowRetry: true);
                        
                        if (errorResult.IsRetry)
                        {
                            // Retry trigger.
                            await _triggerRepository.CreateTriggerAsync(
                                key: Guid.NewGuid().ToString(),
                                timerDate: errorResult.Timeout,
                                processId: elem2.Process.Id,
                                handlerKey: WakeupTriggerRangeHandler<TId>.Name,
                                kind: Model.Abstract.TriggerModule.Components.ITriggerComponent<TId>.TriggerKind.Timer,
                                priority: elem2.Process.Process.Info.Priority,
                                isActivated: true,
                                counter: null,                                
                                cancellationToken);
                        }
                    }
                }
            }
        }

        protected virtual MessageDto BuildMessage(
            IProcessContainer<TId> process,
            IOutboxComponent<TId> outbox,
            IOutboxMessageComponent<TId> messageComponent)
        {
            return new MessageDto(
                messageComponent.Key,
                outbox.Queue,
                messageComponent.Headers.Deserialize<HeaderDto[]>() ?? Array.Empty<HeaderDto>(),
                messageComponent.Body,
                messageComponent.Partition
                );
        }
    }
}
