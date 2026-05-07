using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Components;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.WakeupModule.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Storage
{
    public class EFInboxDbProvider<TId>
        : IProcessDbProvider<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEFDbContext _dbContext;
        private readonly InboxRegistryDto _inboxRegistryDto;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly ITriggerEventRaiser<TId> _triggerEventRaiser;

        private readonly Options _options;
        private readonly EFChangeTrackerProcessRepository<TId, ProcessDbEntity<TId>>.Options _repositoryOptions;        

        private readonly IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> _processDbEntityConditions;
        private readonly IProcessLinkedConditions<TId, InboxProcessDataDbEntity<TId>> _processLinkedConditions;
        private readonly IMessageStreamConditions<TId, InboxMessageDbEntity<TId>> _messageStreamConditions;

        public EFInboxDbProvider(
            IServiceProvider serviceProvider,
            IDateTimeProvider dateTimeProvider,
            IEFDbContext dbContext,
            InboxRegistryDto inboxRegistryDto,
            ILockQueryHintStore lockQueryHintStore,
            ITriggerEventRaiser<TId> triggerEventRaiser,
            Options options,
            EFChangeTrackerProcessRepository<TId, ProcessDbEntity<TId>>.Options repositoryOptions,

            IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> processDbEntityConditions,
            IProcessLinkedConditions<TId, InboxProcessDataDbEntity<TId>> processLinkedConditions,
            IMessageStreamConditions<TId, InboxMessageDbEntity<TId>> messageStreamConditions
            )
        {
            _serviceProvider = serviceProvider;
            _dateTimeProvider = dateTimeProvider;
            _dbContext = dbContext;
            _inboxRegistryDto = inboxRegistryDto;
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
            // TODO: !Критично. если это асинхронное выполнение и ReTry то будет перезагрузка после ошибки. Нужно проверить такой сценарий.

            if (isAsyncExecution)
            {
                // Данные уже загружены в LoadProcessForAsyncProcessingAsync.
                return;
            }

            if (!byTypeIndex.TryGetValue(_inboxRegistryDto.Registry.ProcessType, out var inboxProcessesIds))
            {
                return;
            }

            // 1) Загружаем данные процесса.
            var inboxData = await _dbContext.Set<InboxProcessDataDbEntity<TId>>()
                .Include(e => e.Queue)
                .Include(e => e.Aggregate)
                .ApplayQueryCondition(_processLinkedConditions.ProcessId.QueryRange, inboxProcessesIds)
                .ToDictionaryAsync(e => e.ProcessId, e => e, cancellationToken);

            // 2) Загружаем сообщения по процессам.
            //var messages = await _dbContext.Set<InboxMessageDbEntity<TId>>()
            //    .ApplayQueryCondition(
            //        _messageStreamConditions.ForProcessing.Query, 
            //        new IMessageStreamConditions<TId, InboxMessageDbEntity<TId>>.ForProcessingParamDto(
            //            inboxProcesses,
            //            WithPriorityOrdering: true
            //            )
            //        )
            //    .Take(limit)
            //    .ToArrayAsync(cancellationToken);

            //var messagesByStream = messages
            //    .GroupBy(e => e.ProcessId)
            //    .ToDictionary(e => e.Key, e => e);

            foreach (var elem in inboxProcessesIds)
            {
                var process = processes[elem];

                var component = new EFInboxComponentProxy<TId>(
                    inboxData[process.Id],
                    [] // messagesByStream[process.Id].Select(e => (IInboxMessageComponent<TId>)new EFInboxMessageProxy<TId>(e)).ToArray()
                    );
                process.AddComponent<IInboxComponent<TId>>(component);
            }            

            // 4) В других loader могут загружаться сущности по агрегатам (Range), или загружать уже в самом хендлеое (Single).
        }

        public async Task LoadProcessForAsyncProcessingAsync(
            IDictionary<TId, ProcessInstanceInfoDto<TId>> notLoadedProcesses,
            IDictionary<TId, IProcessContainer<TId>> loadBuffer,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            if (!byTypeIndex.TryGetValue(_inboxRegistryDto.Registry.ProcessType, out var inboxProcessesIds))
            {
                return;
            }

            var notProcessedInboxProcessesIds = inboxProcessesIds.ToHashSet();

            var softTimeout = _repositoryOptions.SoftTimeout.HasValue
                ? DateTimeOffset.Now + _repositoryOptions.SoftTimeout.Value
                : (DateTimeOffset?)null;

            var messagesLimit = _options.MessageLimitFunc(notProcessedInboxProcessesIds.Count);
            int selectedMessages;

            (ProcessDbEntity<TId> Process, IEnumerable<InboxMessageDbEntity<TId>> Messages)[] processGroups;
            {
                var limit = _options.MessageLimitFunc(notProcessedInboxProcessesIds.Count);

                var messages = await _dbContext.Set<InboxMessageDbEntity<TId>>()
                    .ApplayQueryCondition(
                        _messageStreamConditions.ForProcessing.QueryIds,
                        new IMessageStreamConditions<TId, InboxMessageDbEntity<TId>>.ForProcessingParamDto2(notProcessedInboxProcessesIds)
                        )
                    .Take(limit)
                    .ToArrayAsync();
                selectedMessages = messages.Length;

                using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
                {
                    var messageByProcess = messages.GroupBy(e => e.ProcessId).ToArray();

                    var processes = await _dbContext.Set<ProcessDbEntity<TId>>()
                        .Where(e => e.Status == ProcessStatusEnum.AsyncExecute)
                        .Where(e => messageByProcess.Select(e => e.Key).Contains(e.Id))
                        .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

                    processGroups = messageByProcess
                        .Select(e => (processes[e.Key], e.Select(e => e)))
                        .ToArray();
                }
            }

            var processData = await _dbContext.Set<InboxProcessDataDbEntity<TId>>()
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
                        haveErrorOnStart: elem.Process.StoppedByError || elem.Process.RetryCount.HasValue // TODO: condition                                                                                                   
                        ),
                    isAsyncExecuting: true,
                    wakeupState: WakeupStateEnum.NoWakeup
                    );
                if (softTimeout.HasValue)
                {
                    container.AddComponent<ISoftTimeoutComponent>(
                        new SoftTimeoutComponent(softTimeout));
                }
                var component = new EFInboxComponentProxy<TId>(
                    processDataElem,
                    elem.Messages
                        .Select(e => (IInboxMessageComponent<TId>)new EFInboxMessageProxy<TId>(e))
                        .ToArray()
                    );
                container.AddComponent<IInboxComponent<TId>>(component);
                container.AddComponent<IStreamTriggerComponent>(
                    new StreamTriggerComponent(_inboxRegistryDto.TriggerEventQueue, [processDataElem.WakeupTriggerKey]));
                container.AddComponent<IOffsetTriggerComponent>(
                    new OffsetStreamTriggerComponent(_inboxRegistryDto.TriggerEventQueue));

                loadBuffer.Add(container.Id, container);

                notLoadedProcesses.Remove(elem.Process.Id);
                notProcessedInboxProcessesIds.Remove(elem.Process.Id);
            }


            if (notProcessedInboxProcessesIds.Any())
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
                        var messageStreamConditions = scope.ServiceProvider.GetRequiredService<IMessageStreamConditions<TId, InboxMessageDbEntity<TId>>>();
                        var triggerEventRaiser = scope.ServiceProvider.GetRequiredService<ITriggerEventRaiser<TId>>();

                        await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                        {
                            var haveChanges = false;
                            var triggerEvents = new List<ITriggerEventRaiser<TId>.RaiseContainer>(0);
                            using (var _ = queryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
                            {
                                var messageQuery = _dbContext.Set<InboxMessageDbEntity<TId>>()
                                    .ApplayQueryCondition(messageStreamConditions.IsActiveMessages.Query);

                                var activeWithoutMessages = await dbContext.Set<ProcessDbEntity<TId>>()
                                    .Join(
                                        dbContext.Set<ProcessWakeupDbEntity<TId>>(),
                                        e => e.Id,
                                        e => e.ProcessId,
                                        (e1, e2) => new { Process = e1, Wakeup = e2 }
                                    )
                                    .Join(
                                        dbContext.Set<InboxProcessDataDbEntity<TId>>(),
                                        e => e.Process.Id,
                                        e => e.ProcessId,
                                        (e1, e2) => new { Process = e1.Process, Wakeup = e1.Wakeup, Data = e2 }
                                        )
                                    .Where(e => notProcessedInboxProcessesIds.Contains(e.Process.Id))
                                    .Where( e => !messageQuery.Any(e2 => e2.ProcessId.Equals(e.Process.Id)))
                                    .ToArrayAsync(cancellationToken);

                                if (activeWithoutMessages.Any())
                                {
                                    triggerEvents.Capacity = activeWithoutMessages.Length * 2;

                                    var pd = await dbContext.Set<InboxProcessDataDbEntity<TId>>()
                                        .Where(e => activeWithoutMessages.Select(e => e.Process.Id)
                                        .Contains(e.ProcessId))
                                        .ToDictionaryAsync(e => e.ProcessId, e => e);

                                    haveChanges = true;
                                    foreach (var elem in activeWithoutMessages)
                                    {
                                        if (elem.Process.Status == ProcessStatusEnum.AsyncExecute)
                                        {
                                            elem.Process.SelectLockTimeout = _dateTimeProvider.UtcNow;
                                            elem.Process.Status = ProcessStatusEnum.WaitEvent;
                                            elem.Wakeup.IsAsyncExecuting = false;

                                            notLoadedProcesses.Remove(elem.Process.Id);
                                            notProcessedInboxProcessesIds.Remove(elem.Process.Id);

                                            var elemProcessData = pd[elem.Process.Id];
                                            // По хорощему не нужно, но на случай, если событие было утеряно.
                                            triggerEvents.Add(
                                                new ITriggerEventRaiser<TId>.RaiseContainer(
                                                    _inboxRegistryDto.TriggerEventQueue,
                                                    elem.Process.Id,
                                                    new ProcessedOffsetTriggerEvent(
                                                    elemProcessData.WakeupTriggerKey,
                                                    processedOffset: elemProcessData.ProcessedOffset
                                                    )
                                                    ));
                                            triggerEvents.Add(
                                                new ITriggerEventRaiser<TId>.RaiseContainer(
                                                    _inboxRegistryDto.TriggerEventQueue,
                                                    elem.Process.Id,
                                                    new ProcessGoWaitStreamTriggerEvent(
                                                        elemProcessData.WakeupTriggerKey
                                                        )
                                                    ));                                            
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

                if (notProcessedInboxProcessesIds.Any())
                {
                    //// Процессы попали в селектор, но не уложились в лимит сообщений.
                    // Снимаем select lock, чтобы другая нода могла брать их в обработку.
                    // В отдельной транзакции потому, что нужно сделать сейчас, а не в конце основной транзакции.
                    await using (var scope = _serviceProvider.CreateAsyncScope())
                    {
                        var selectQuery = scope.ServiceProvider.GetRequiredService<IProcessAsyncProcessingSelectQuery<TId>>();

                        await selectQuery.UnlockSelectAsync(
                            notProcessedInboxProcessesIds,
                            cancellationToken);
                    }

                    // Чтобы основной загрузчик не пытался их загрузить.
                    foreach (var elem in notProcessedInboxProcessesIds)
                    {
                        notLoadedProcesses.Remove(elem);
                    }
                }
            }
        }

        public async Task LoadRangeAsync(
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
