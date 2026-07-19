using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Retry;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.CommonModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Components;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.OutboxModule.Services
{
    public class OutboxRangeProcessHandler2<TId>
        : BaseRangeProcessHandler<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly OptionsDto _options;

        public override ExecuteStepByStepGroupMiddleware<TId>.OptionsDto Options { get; }


        public OutboxRangeProcessHandler2(
            IProcessRepository<TId> repository,
            ITriggerRepository<TId> triggerRepository,
            IProcessSetter processSetter,
            IServiceProvider serviceProvider,
            IDateTimeProvider dateTimeProvider,
            OptionsDto options,
            ExecuteStepByStepGroupMiddleware<TId>.OptionsDto stepByStepOptions)
            : base(
                  repository,
                  triggerRepository,
                  processSetter)
        {
            _serviceProvider = serviceProvider;
            _dateTimeProvider = dateTimeProvider;
            _options = options;
            Options = stepByStepOptions;
        }

        public override async ValueTask StepRangeAsync(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group, 
            CancellationToken cancellationToken)
        {
            DateTimeOffset? softTimeout = null;

            var context = group.Group.Values
                .Select(
                e => 
                {
                    softTimeout = SoftTimeoutHelper.Min(
                        softTimeout, 
                        e.GetComponent<ISoftTimeoutComponent>());

                    return new ContextEntry(e, e.GetComponent<IOutboxComponent<TId>>(), false);
                })
                .ToDictionary(e => e.Process.Id, e => e);

            var isExecuting = LinkContainer.Create(true);
            var currentCycle = LinkContainer.Create(0);
            var cycleLimit = LinkContainer.Create(_options.CycleLimit ?? int.MaxValue);

            await SoftTimeoutHelper.ExecuteWithSoftTimeoutAsync(
                (_serviceProvider, context, isExecuting, currentCycle, cycleLimit),
                _dateTimeProvider,
                softTimeout,
                static (p) => 
                    p.isExecuting.Data 
                    || p.currentCycle.Data >= p.cycleLimit.Data,
                static async (p, t) => 
                {
                    // Отдельный scope для малой транзакции.
                    await using var scope = p._serviceProvider.CreateAsyncScope();
                    await CycleAsync(scope.ServiceProvider, p.context, p.isExecuting, t);

                    p.currentCycle.Data++;
                },
                cancellationToken);
            
            foreach (var elem in context.Values)
            {
                // Если на момент проверки сообщений не было, то в ожидание.
                if (elem.NotHaveMessages)
                {
                    _processSetter.SetStatus(elem.Process, ProcessStatusEnum.WaitEvent);
                }
            }
        }

        private static async Task CycleAsync(
            IServiceProvider serviceProvider,
            Dictionary<TId, ContextEntry> context,
            LinkContainer<bool> isExecuting,
            CancellationToken cancellationToken)
        {
            var options = serviceProvider.GetRequiredService<OptionsDto>();
            var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
            var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
            var queueProviderFactory = serviceProvider.GetRequiredService<IQueueProviderFactory>();
            var query = serviceProvider.GetRequiredService<IQueries>();
            var triggerRepository = serviceProvider.GetRequiredService<ITriggerRepository<TId>>();
            var setter = serviceProvider.GetRequiredService<IProcessSetter>();
            var headerJsonSerializer = serviceProvider.GetRequiredService<IHeaderJsonSerializer>();
            
            await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
            {
                if (!context.Any())
                {
                    isExecuting.Data = false;
                    return;
                }

                var messages = await query.LoadMessagesForProcessingAsync(
                    context.Keys,
                    options.TransactionBatchSize,
                    cancellationToken);

                if (!messages.Any())
                {
                    // Необработанных сообщений нет.
                    foreach (var elem in context)
                    {
                        elem.Value.NotHaveMessages = true;
                    }

                    isExecuting.Data = false;
                    return;
                }                

                var processMessageGroups = messages
                    .GroupBy(e => e.ProcessId)
                    .ToDictionary(e => e.Key, e => e);
                
                var isLessLimit = messages.Length < options.TransactionBatchSize;
                if (isLessLimit)
                {
                    // Часть процессов возможно не содержат сообщений.
                    foreach (var elem in context.Values)
                    {
                        if (!processMessageGroups.ContainsKey(elem.Process.Id))
                        {
                            elem.NotHaveMessages = true;
                        }
                        else 
                        {
                            elem.NotHaveMessages = false;
                        }
                    }

                    isExecuting.Data = false;
                }
                else 
                {
                    // Лимит заполнен, мы не можем сказать, что у кого-то нет сообщений.
                    foreach (var elem in context.Values)
                    {
                        elem.NotHaveMessages = false;
                    }
                }

                {
                    var haveMessageGroupByQueue = context.Values
                        .Where(e => processMessageGroups.ContainsKey(e.Process.Id))
                        .GroupBy(e => e.OutboxComponent.Queue)
                        .ToArray();

                    var retryBuffer = new List<ITriggerRepository<TId>.CreateTriggerDto>(context.Count);
                    foreach (var elem in haveMessageGroupByQueue)
                    {
                        var queueBatch = elem
                            .SelectMany(
                                e => processMessageGroups[e.Process.Id]
                                    .Select(e2 => (e.Process, e.OutboxComponent, Message: e2))
                                )
                            .OrderByDescending(e => e.Message.Priority)
                            .ThenBy(e => e.Message.OrderId)
                                .Select(e => (
                                    Message: e,
                                    producerMessage: BuildMessage(headerJsonSerializer, e.Process, e.OutboxComponent, e.Message)
                                    ))
                            .ToArray();

                        try
                        {
                            var producer = await queueProviderFactory.GetProducerAsync(elem.Key, cancellationToken);

                            await producer.ProduceBatchAsync(
                                queueBatch.Select(e => e.producerMessage).ToArray(),
                                cancellationToken);

                            foreach (var elem2 in queueBatch)
                            {
                                elem2.Message.Message.IsActive = false;
                                elem2.Message.Message.SendDate = dateTimeProvider.UtcNow;
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
                                var errorResult = setter.SetError(elem2.Process, ex, allowRetry: true);

                                // Retry trigger.
                                if (errorResult.IsRetry)
                                {
                                    retryBuffer.Add(
                                        ITriggerRepository<TId>.CreateTriggerDto.TimerTrigger(
                                            key: Guid.NewGuid().ToString(),
                                            timerDate: errorResult.Timeout,
                                            processId: elem2.Process.Id,
                                            isRangeTrigger: true,
                                            handlerKey: NoWakeupRetryTriggerRangeHandler<TId>.Name,
                                            priority: elem2.Process.Process.Info.Priority,
                                            isActivated: true,
                                            isChildTrigger: false));
                                }

                                context.Remove(elem2.Process.Id);
                            }
                        }
                    }

                    if (retryBuffer.Any())
                    {
                        await triggerRepository.CreateTriggerRangeAsync(
                            retryBuffer,
                            cancellationToken);
                    }
                }

                await query.UpdateMessagesAsync(messages, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
        }

        protected static MessageDto BuildMessage(
            IHeaderJsonSerializer headerJsonSerializer,
            IProcessContainer<TId> process,
            IOutboxComponent<TId> outbox,
            IOutboxMessageComponent<TId> messageComponent)
        {
            return MessageDto.ForSend(
                messageComponent.Key,
                outbox.Queue,
                headerJsonSerializer.Deserialize(messageComponent.Headers),
                messageComponent.Body,
                messageComponent.Partition
                );
        }

        #region

        private class ContextEntry 
        {
            public IProcessContainer<TId> Process { get; }

            public IOutboxComponent<TId> OutboxComponent { get; }

            public bool NotHaveMessages { get; set; }

            public ContextEntry(
                IProcessContainer<TId> process, 
                IOutboxComponent<TId> outboxComponent, 
                bool haveNotProcessedMessages)
            {
                Process = process;
                OutboxComponent = outboxComponent;
                NotHaveMessages = haveNotProcessedMessages;
            }
        }

        public class OptionsDto 
        {
            public int TransactionBatchSize { get; set; }
                = 100;

            public int? CycleLimit { get; set; }
        }

        public interface IQueries
        {
            Task<IOutboxMessageComponent<TId>[]> LoadMessagesForProcessingAsync(
                ICollection<TId> processIds,
                int batchSize,
                CancellationToken cancellationToken);

            Task<HashSet<TId>> NotProcessedMessagesExsistsAsync(
                ICollection<TId> processIds,
                CancellationToken cancellationToken);

            Task UpdateMessagesAsync(
                IOutboxMessageComponent<TId>[] messages,
                CancellationToken cancellationToken);
        }

        #endregion
    }
}
