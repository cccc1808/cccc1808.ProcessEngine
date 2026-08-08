using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Retry;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.CommonModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Services;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.OutboxModule.Services
{
    /// <summary>
    /// Outbox process -> queue.
    /// Сообщение предзагружается в DbProvider.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class OutboxRangeProcessHandler1<TId>
        : BaseRangeProcessHandler<TId>
    {
        private readonly IQueueProviderFactory _queueProviderFactory;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IOutboxSetter _outboxSetter;
        private readonly IHeaderJsonSerializer _headerJsonSerializer;

        public OutboxRangeProcessHandler1(
            IProcessRepository<TId> repository,
            ITriggerRepository<TId> triggerRepository,
            IProcessSetter setter,
            IQueueProviderFactory queueProviderFactory,
            IDateTimeProvider dateTimeProvider,
            IOutboxSetter outboxSetter,
            IHeaderJsonSerializer headerJsonSerializer,
            ExecuteStepByStepGroupMiddleware<TId>.OptionsDto options)
            : base(
                  repository,
                  triggerRepository,
                  setter)
        {
            _queueProviderFactory = queueProviderFactory;
            _dateTimeProvider = dateTimeProvider;
            _outboxSetter = outboxSetter;
            _headerJsonSerializer = headerJsonSerializer;
            Options = options;
        }

        public override ExecuteStepByStepGroupMiddleware<TId>.OptionsDto Options { get; }

        public override async ValueTask StepRangeAsync(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group,
            CancellationToken cancellationToken)
        {            
            var softTimeoutDate = DateTimeOffset.MaxValue;
            var context = group.Group
                .Select(e => 
                    {
                        if (e.Value.TryGetComponent<ISoftTimeoutComponent>(out var component))
                        {
                            softTimeoutDate = SoftTimeoutHelper.Min(softTimeoutDate, component);
                        }

                        return (
                            Process: e.Value,
                            Outbox: e.Value.GetComponent<IOutboxComponent<TId>>());
                            }
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
                // Soft timeout
                if (softTimeoutDate < _dateTimeProvider.UtcNow)
                {
                    foreach (var elem2 in group.Group.Values)
                    {
                        _processSetter.StopAsyncProcessingSession(elem2, true);
                    }

                    break;
                }

                var queueBatch = elem
                    .SelectMany(e1 => e1.Outbox.Messages
                        .Select(e2 => (e1.Process, e1.Outbox, Message: e2))
                        )
                    .OrderByDescending(e => e.Message.Priority)
                    .ThenBy(e => e.Message.OrderId)
                    .Select(e => (
                        Message: e,
                        producerMessage: BuildMessage(e.Process, e.Outbox, e.Message)
                        ))
                    .ToArray();

                // TODO: Для надежности можно добавить другую обработку CanclationToken.
                // Чтобы в случае gracefull остановки, доработать процессы по которым выполнена отправка (не было дублирующей отправки).
                var producer = await _queueProviderFactory.GetProducerAsync(elem.Key, cancellationToken);
                try
                {
                    await producer.ProduceBatchAsync(
                        queueBatch.Select(e => e.producerMessage).ToArray(),
                        cancellationToken);

                    foreach (var elem2 in queueBatch)
                    {
                        _outboxSetter.OutboxMessageProcessed(
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

                    var uniqueProcesses = elem.Select(e => e.Process).Distinct().ToArray();
                    var retryBuffer = new List<ITriggerRepository<TId>.CreateTriggerDto>(uniqueProcesses.Length);
                    foreach (var elem2 in uniqueProcesses)
                    {
                        var errorResult = _processSetter.SetError(elem2, ex, allowRetry: true);

                        // Retry trigger.
                        if (errorResult.IsRetry)
                        {
                            retryBuffer.Add(
                                ITriggerRepository<TId>.CreateTriggerDto.TimerTrigger(
                                    key: Guid.NewGuid().ToString(),
                                    timerDate: errorResult.Timeout,
                                    processId: elem2.Id,
                                    isRangeTrigger: true,
                                    handlerKey: NoWakeupRetryTriggerRangeHandler<TId>.Name,
                                    priority: elem2.Process.Info.Registry.Unique.Priority,
                                    isActivated: true,
                                    isChildTrigger: false));
                        }
                    }

                    if (retryBuffer.Any())
                    {
                        await _triggerRepository.CreateTriggerRangeAsync(
                            retryBuffer,
                            cancellationToken);
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
                _headerJsonSerializer.Deserialize(messageComponent.Headers),
                messageComponent.Body,
                messageComponent.Partition
                );
        }
    }
}
