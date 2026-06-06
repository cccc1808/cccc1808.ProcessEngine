using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Storage.Queries;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Components;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Storage
{
    public class EFOutboxDbProvider<TId>
        : IProcessDbProvider<TId>
    {       
        private readonly IServiceProvider _serviceProvider;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEFDbContext _dbContext;
        private readonly OutboxRegistryDto _outboxRegistry;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly ITriggerEventRaiser<TId> _triggerEventRaiser;

        private readonly Options _options;
        private readonly EFChangeTrackerProcessRepository<TId, ProcessDbEntity<TId>>.Options _repositoryOptions;

        private readonly IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> _processDbEntityConditions;
        private readonly IProcessLinkedConditions<TId, OutboxProcessDataDbEntity<TId>> _processLinkedConditions;
        private readonly IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>> _messageStreamConditions;

        public EFOutboxDbProvider(
            IServiceProvider serviceProvider,
            IDateTimeProvider dateTimeProvider,
            IEFDbContext dbContext, 
            OutboxRegistryDto outboxRegistry,
            ILockQueryHintStore lockQueryHintStore,
            ITriggerEventRaiser<TId> triggerEventRaiser,

            Options options,
            EFChangeTrackerProcessRepository<TId, ProcessDbEntity<TId>>.Options repositoryOptions,

            IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> processDbEntityConditions,
            IProcessLinkedConditions<TId, OutboxProcessDataDbEntity<TId>> processLinkedConditions,
            IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>> messageStreamConditions)
        {
            _serviceProvider = serviceProvider;
            _dateTimeProvider = dateTimeProvider;
            _dbContext = dbContext;
            _outboxRegistry = outboxRegistry;
            _lockQueryHintStore = lockQueryHintStore;
            _triggerEventRaiser = triggerEventRaiser;

            _options = options;
            _repositoryOptions = repositoryOptions;

            _processDbEntityConditions = processDbEntityConditions;
            _processLinkedConditions = processLinkedConditions;
            _messageStreamConditions = messageStreamConditions;
        }

        public async Task LoadProcessDataAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            bool isAsyncExecution,
            CancellationToken cancellationToken)
        {
            if (isAsyncExecution)
            {
                // Данные уже загружены в LoadProcessForAsyncProcessingAsync.
                return;
            }

            if (!byTypeIndex.TryGetValue(_outboxRegistry.Registry.ProcessType, out var outboxProcessesIds))
            {
                return;
            }

            // 1) Загружаем данные процесса.
            var outboxData = await _dbContext.Set<OutboxProcessDataDbEntity<TId>>()
                .Include(e => e.Queue)
                .ApplayQueryCondition(_processLinkedConditions.ProcessId.QueryRange, outboxProcessesIds)
                .ToDictionaryAsync(e => e.ProcessId, e => e, cancellationToken);

            // 2) Загружаем сообщения по процессам.
            //var messages = await _dbContext.Set<OutboxMessageDbEntity<TId>>()
            //    .ApplayQueryCondition(
            //        _messageStreamConditions.ForProcessing.Query,
            //        new IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>>.ForProcessingParamDto2(
            //            outboxProcesses, 
            //            WithPriorityOrdering: true
            //            )
            //        )
            //    .Take(_messagesLimit)
            //    .ToArrayAsync(cancellationToken);

            //var messagesByStream = messages
            //    .GroupBy(e => e.ProcessId)
            //    .ToDictionary(e => e.Key, e => e);

            foreach (var elem in outboxProcessesIds)
            {
                var process = processes[elem];
                var component = new EFOutboxComponentProxy<TId>(
                    outboxData[process.Id],
                    [] //messagesByStream[process.Id].Select(e => new EFOutboxMessageProxy<TId>(e)).ToArray()                    
                    );
                process.AddComponent<IOutboxComponent<TId>>(component);
            }

            // 4) В других DbProvider могут загружаться сущности по агрегатам (Range), или загружать уже в самом хендлеое (Single).
        }

        public async Task LoadProcessForAsyncProcessingAsync(
            IDictionary<TId, ProcessInstanceInfoDto<TId>> notLoadedProcesses, 
            IDictionary<TId, IProcessContainer<TId>> loadBuffer, 
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex, 
            CancellationToken cancellationToken)
        {
            if (!byTypeIndex.TryGetValue(_outboxRegistry.Registry.ProcessType, out var outboxProcessesIds))
            {
                return;
            }

            var notProcessedOutboxProcessesIds = outboxProcessesIds.ToHashSet();

            var softTimeout = _repositoryOptions.SoftTimeout.HasValue
                ? DateTimeOffset.Now + _repositoryOptions.SoftTimeout.Value
                : (DateTimeOffset?)null;

            var messagesLimit = _options.MessageLimitFunc(notProcessedOutboxProcessesIds.Count);
            int selectedMessages;
            (ProcessDbEntity<TId> Process, IEnumerable<OutboxMessageDbEntity<TId>> Messages)[] processGroups;
            using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                // В1: нормальный join
                var idsQuery = _dbContext
                    .QueryFromCollection(notProcessedOutboxProcessesIds.Select(
                        e => new
                        {
                            ProcessTypeId = _outboxRegistry.Registry.ProcessType.ProcessType,
                            ProcessVersion = _outboxRegistry.Registry.ProcessType.ProcessVersion,
                            Priority = _outboxRegistry.Registry.Priority,
                            Id = e,
                        })
                    .ToArray());
                var query = _dbContext.Set<ProcessDbEntity<TId>>()
                    .Join(
                        idsQuery,
                        e => new { e.ProcessTypeId, e.ProcessVersion, e.Priority, e.Id },
                        e => e,
                        (e1, e2) => e1)
                    .Join(
                        _dbContext.Set<OutboxMessageDbEntity<TId>>(),
                        e => e.Id,
                        e => e.ProcessId,
                        (e1, e2) => new { Process = e1, Message = e2 });
                query = query
                    .ApplayQueryCondition(
                        _processDbEntityConditions.DbProcessingForHandlerProjection(query),
                        e => e.Process,
                        new IProcessDbEntityConditions<TId, ProcessDbEntity<TId>>.DbProcessingForHandlerParameters(
                            _dbContext,
                            [_outboxRegistry.Registry],
                            notProcessedOutboxProcessesIds))
                    .ApplayQueryCondition(
                        _messageStreamConditions.ForProcessingProjection(query),
                        e => e.Message,
                        new IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>>.ForProcessingParamDto1()
                    );
                var data = await query
                    .OrderByDescending(e => e.Message.Priority)
                    .ThenBy(e => e.Message.OrderId)
                    .ToArrayAsync(cancellationToken);
                selectedMessages = data.Length;

                // В2 Коррелированный подзапрос
                //var processQuery = _dbContext.Set<ProcessDbEntity<TId>>()
                //    .ApplayQueryCondition(
                //    _processDbEntityConditions.DbProcessingForHandler.Query,
                //    new IProcessDbEntityConditions<TId, ProcessDbEntity<TId>>.DbProcessingForSelectorHandlerParameters(
                //        _dbContext,
                //        [_outboxRegistry.Registry],
                //        notLoadedProcesses.Keys));
                //var messagesQuery = _dbContext.Set<OutboxMessageDbEntity<TId>>()
                //    .ApplayQueryCondition(
                //        _messageStreamConditions.ForProcessing.Query,
                //        new IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>>.ForProcessingParamDto1()
                //        );
                //var data = await processQuery
                //    .Join(messagesQuery, e => e.Id, e => e.ProcessId, (process, message) => new { process, message })
                //    .Take(limit)
                //    .ToArrayAsync(cancellationToken);

                processGroups = data
                    .GroupBy(e => e.Process.Id)
                    .Select(e => (e.First().Process, e.Select(e => e.Message)))
                    .ToArray();
            }

            var processData = await _dbContext.Set<OutboxProcessDataDbEntity<TId>>()
                .Include(e => e.Queue)
                .Where(e => processGroups.Select(e => e.Process.Id).Contains(e.ProcessId))
                .ToDictionaryAsync(e => e.ProcessId, e => e);

            foreach (var elem in processGroups)
            {
                // Так как мы уже считали с блокировкой,
                // то в конце текущей транзакции тожно сбросить SelectLock, т.к. сессия работы была завершена.
                // Не сбрасываем на min, потому что значение используется.
                elem.Process.SelectLockTimeout = _dateTimeProvider.UtcNow;
                var processDataElem = processData[elem.Process.Id];

                var container = new ProcessContainer<TId>(
                    new EFProcessProxyComponent<TId>(elem.Process),
                    new AsyncSessionComponent(
                        sessionId: Guid.Empty,
                        isSessionFirstStep: true,
                        currentSessionHaveError: false,
                        retryLimit: _repositoryOptions.RetryLimit,
                        stopAsyncProcessingSession: false,
                        needUpdateErrorData: false,
                        haveErrorOnStart: elem.Process.StoppedByError || elem.Process.RetryCount.HasValue, // TODO: condition                                                                                                   
                        clearErrorOnSessionEnd: true
                        ),
                    isAsyncExecuting: true,
                    wakeupState: WakeupStateEnum.CheckWakeupWithoutLock                       
                    );
                if (softTimeout.HasValue)
                {
                    container.AddComponent<ISoftTimeoutComponent>(
                        new SoftTimeoutComponent(softTimeout));
                }
                var component = new EFOutboxComponentProxy<TId>(
                    processDataElem,
                    elem.Messages
                        .Select(e => (IOutboxMessageComponent<TId>)new EFOutboxMessageProxy<TId>(e))
                        .ToArray()
                    );
                container.AddComponent<IOutboxComponent<TId>>(component);
                container.AddComponent<IStreamTriggerComponent>(
                    new StreamTriggerComponent(
                        _outboxRegistry.TriggerEventQueue,
                        [processDataElem.WakeupTriggerKey]));

                loadBuffer.Add(container.Id, container);

                notLoadedProcesses.Remove(elem.Process.Id);
                notProcessedOutboxProcessesIds.Remove(elem.Process.Id);
            }            

            if (notProcessedOutboxProcessesIds.Any())
            {
                if (selectedMessages < messagesLimit)
                {
                    // Это означает, что есть активные процессы, в которых нет сообщений.
                    // Пробуем отправить спать.
                    await using (var scope = _serviceProvider.CreateAsyncScope())
                    {
                        var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
                        var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                        var queryHintStore = scope.ServiceProvider.GetRequiredService<ILockQueryHintStore>();
                        var messageStreamConditions = scope.ServiceProvider.GetRequiredService<IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>>>();
                        var triggerEventRaiser = scope.ServiceProvider.GetRequiredService<ITriggerEventRaiser<TId>>();

                        await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                        {
                            var haveChanges = false;
                            var triggerEvents = new List<ITriggerEventRaiser<TId>.RaiseContainer>(0);
                            using (var _ = queryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
                            {
                                var messageQuery = _dbContext.Set<OutboxMessageDbEntity<TId>>()
                                   .ApplayQueryCondition(messageStreamConditions.IsActiveMessages.Query);

                                var activeWithoutMessages = await dbContext.Set<ProcessDbEntity<TId>>()
                                    .Join(
                                        dbContext.Set<ProcessWakeupDbEntity<TId>>(),
                                        e => e.Id,
                                        e => e.ProcessId,
                                        (e1, e2) => new { Process = e1, Wakeup = e2 }
                                    )
                                    .Join(
                                        dbContext.Set<OutboxProcessDataDbEntity<TId>>(),
                                        e => e.Process.Id,
                                        e => e.ProcessId,
                                        (e1, e2) => new { Process = e1.Process, Wakeup = e1.Wakeup, Data = e2 }
                                        )
                                    .Where(e => notProcessedOutboxProcessesIds.Contains(e.Process.Id))
                                    .Where(e => !messageQuery.Any(e2 => e2.ProcessId.Equals(e.Process.Id)))
                                    .ToArrayAsync(cancellationToken);

                                if (activeWithoutMessages.Any())
                                {
                                    haveChanges = true;
                                    triggerEvents.Capacity = activeWithoutMessages.Length;

                                    var pd = await dbContext.Set<OutboxProcessDataDbEntity<TId>>()
                                        .Where(e => activeWithoutMessages.Select(e => e.Process.Id)
                                        .Contains(e.ProcessId))
                                        .ToDictionaryAsync(e => e.ProcessId, e => e);

                                    foreach (var elem in activeWithoutMessages)
                                    {
                                        if (elem.Process.Status == ProcessStatusEnum.AsyncExecute)
                                        {
                                            elem.Process.SelectLockTimeout = _dateTimeProvider.UtcNow;
                                            elem.Process.Status = ProcessStatusEnum.WaitEvent;
                                            elem.Wakeup.IsAsyncExecuting = false;

                                            notLoadedProcesses.Remove(elem.Process.Id);
                                            notProcessedOutboxProcessesIds.Remove(elem.Process.Id);

                                            triggerEvents.Add(
                                                new ITriggerEventRaiser<TId>.RaiseContainer(
                                                    _outboxRegistry.TriggerEventQueue,
                                                    elem.Process.Id,
                                                    new ProcessGoWaitStreamTriggerEvent(
                                                        pd[elem.Process.Id].WakeupTriggerKey
                                                        )
                                                    )
                                                );
                                        }
                                    }
                                }

                            }

                            if (haveChanges)
                            {
                                await _triggerEventRaiser.RaiseAsync(
                                    triggerEvents,
                                    cancellationToken
                                    );

                                await dbContext.SaveChangesAsync(cancellationToken);
                            }

                            await transaction.CommitAsync(cancellationToken);
                        }
                    }
                }

                if (notProcessedOutboxProcessesIds.Any())
                {
                    //// Процессы попали в селектор, но не уложились в лимит сообщений.
                    // Снимаем select lock, чтобы другая нода могла брать их в обработку.
                    // В отдельной транзакции потому, что нужно сделать сейчас, а не в конце основной транзакции.
                    await using (var scope = _serviceProvider.CreateAsyncScope())
                    {
                        var unreserveProcessQuery = scope.ServiceProvider.GetRequiredService<IUnreserveProcessQuery<TId>>();

                        await unreserveProcessQuery.UnreserveAsync(
                            notProcessedOutboxProcessesIds,
                            cancellationToken);
                    }

                    // Чтобы основной загрузчик не пытался их загрузить.
                    foreach (var elem in notProcessedOutboxProcessesIds)
                    {
                        notLoadedProcesses.Remove(elem);
                    }
                }
            }
        }

        public Task LoadRangeAsync(
            IDictionary<TId, IProcessContainer<TId>> processes, 
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex, 
            bool withLock,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            // EF дополнительное сохранение не нужно.
            return Task.CompletedTask;
        }

        public class Options()
        {
            /// <summary>
            /// Ограничение количества загружаемых сообщений на основе количества идентификаторов процессов в батче.
            /// </summary>
            public Func<int, int> MessageLimitFunc { get; set; }
                = (e) => e * 50;
        }
    }
}
