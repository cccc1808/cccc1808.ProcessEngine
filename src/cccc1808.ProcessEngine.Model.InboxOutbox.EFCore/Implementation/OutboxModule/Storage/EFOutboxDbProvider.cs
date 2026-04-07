using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Components;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Storage
{
    internal class EFOutboxDbProvider<TId>
        : IProcessDbProvider<TId>
    {       
        private readonly IServiceProvider _serviceProvider;
        private readonly IEFDbContext _dbContext;
        private readonly OutboxRegistryDto _outboxRegistry;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly Options _options;
        private readonly EFChangeTrackerProcessRepository<TId, ProcessDbEntity<TId>>.Options _repositoryOptions;

        private readonly IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> _processDbEntityConditions;
        private readonly IProcessLinkedConditions<TId, OutboxProcessDataDbEntity<TId>> _processLinkedConditions;
        private readonly IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>> _messageStreamConditions;

        public EFOutboxDbProvider(
            IServiceProvider serviceProvider,
            IEFDbContext dbContext, 
            OutboxRegistryDto outboxRegistry,
            ILockQueryHintStore lockQueryHintStore,
            Options options,
            EFChangeTrackerProcessRepository<TId, ProcessDbEntity<TId>>.Options repositoryOptions,

            IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> processDbEntityConditions,
            IProcessLinkedConditions<TId, OutboxProcessDataDbEntity<TId>> processLinkedConditions,
            IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>> messageStreamConditions)
        {
            _serviceProvider = serviceProvider;
            _dbContext = dbContext;
            _outboxRegistry = outboxRegistry;
            _lockQueryHintStore = lockQueryHintStore;
            _options = options;
            _repositoryOptions = repositoryOptions;

            _processDbEntityConditions = processDbEntityConditions;
            _processLinkedConditions = processLinkedConditions;
            _messageStreamConditions = messageStreamConditions;
        }

        public async Task LoadProcessDataAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            var outboxProcesses = byTypeIndex[_outboxRegistry.Registry.ProcessType];

            // 1) Загружаем данные процесса.
            var outboxData = await _dbContext.Set<OutboxProcessDataDbEntity<TId>>()
                .Include(e => e.Queue)
                .ApplayQueryCondition(_processLinkedConditions.ProcessId.QueryRange, outboxProcesses)
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

            foreach (var elem in outboxProcesses)
            {
                var process = processes[elem];
                var component = new EFOutboxComponentProxy<TId>(
                    outboxData[process.Id],
                    [] //messagesByStream[process.Id].Select(e => new EFOutboxMessageProxy<TId>(e)).ToArray()                    
                    );
                process.AddComponent(component);
            }

            // 4) В других DbProvider могут загружаться сущности по агрегатам (Range), или загружать уже в самом хендлеое (Single).
        }

        public async Task LoadProcessForAsyncProcessingAsync(
            IDictionary<TId, ProcessInstanceInfoDto<TId>> notLoadedProcesses, 
            IDictionary<TId, IProcessContainer<TId>> loadBuffer, 
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex, 
            CancellationToken cancellationToken)
        {
            var outboxProcessesIds = byTypeIndex[_outboxRegistry.Registry.ProcessType]
                .Select(e => notLoadedProcesses[e])
                .ToDictionary(e => e.Id, e => e);

            if (!outboxProcessesIds.Any())
            {
                return;
            }

            var softTimeout = _repositoryOptions.SoftTimeout.HasValue
                ? DateTimeOffset.Now + _repositoryOptions.SoftTimeout.Value
                : (DateTimeOffset?)null;

            (ProcessDbEntity<TId> Process, IEnumerable<OutboxMessageDbEntity<TId>> Messages)[] processGroups;
            using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                var limit = _options.MessageLimitFunc(outboxProcessesIds.Count);

                var processQuery = _dbContext.Set<ProcessDbEntity<TId>>()
                    .ApplayQueryCondition(
                    _processDbEntityConditions.DbProcessingForHandler.Query,
                    new IProcessDbEntityConditions<TId, ProcessDbEntity<TId>>.DbProcessingForSelectorHandlerParameters(
                        _dbContext,
                        [_outboxRegistry.Registry],
                        notLoadedProcesses.Keys));
                var messagesQuery = _dbContext.Set<OutboxMessageDbEntity<TId>>()
                    .ApplayQueryCondition(
                        _messageStreamConditions.ForProcessing.Query,
                        new IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>>.ForProcessingParamDto1(
                            WithPriorityOrdering: true
                            )
                        );

                var data = await processQuery
                    .Join(messagesQuery, e => e.Id, e => e.ProcessId, (process, message) => new { process, message })
                    .Take(limit)
                    .ToArrayAsync(cancellationToken);

                processGroups = data
                    .GroupBy(e => e.process.Id)
                    .Select(e => (e.First().process, e.Select(e => e.message)))
                    .ToArray();
            }

            var processData = await _dbContext.Set<OutboxProcessDataDbEntity<TId>>()
                .Where(e => processGroups.Select(e => e.Process.Id).Contains(e.ProcessId))
                .ToDictionaryAsync(e => e.ProcessId, e => e);

            foreach (var elem in processGroups)
            {
                // Так как мы уже считали с блокировкой,
                // то в конце текущей транзакции тожно сбросить SelectLock, т.к. сессия работы была завершена.
                // Не сбрасываем на min, потому что значение используется.
                elem.Process.SelectLockTimeout = DateTimeOffset.UtcNow;

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
                        )
                       );
                if (softTimeout.HasValue)
                {
                    container.AddComponent<ISoftTimeoutComponent>(
                        new SoftTimeoutComponent(softTimeout));
                }
                var component = new EFOutboxComponentProxy<TId>(
                    processData[elem.Process.Id],
                    elem.Messages
                        .Select(e => (IOutboxMessageComponent<TId>)new EFOutboxMessageProxy<TId>(e))
                        .ToArray()
                    );
                container.AddComponent<IOutboxComponent<TId>>(component);

                loadBuffer.Add(container.Id, container);

                notLoadedProcesses.Remove(elem.Process.Id);
                outboxProcessesIds.Remove(elem.Process.Id);
            }            

            if (outboxProcessesIds.Any())
            {
                // Процессы попали в селектор, но не уложились в лимит сообщений.

                // Снимаем select lock, чтобы другая нода могла брать их в обработку.
                // В отдельной транзакции потому, что нужно сделать сейчас, а не в конце основной транзакции.
                await using (var scope = _serviceProvider.CreateAsyncScope())
                {
                    var selectQuery = scope.ServiceProvider.GetRequiredService<IProcessAsyncProcessingSelectQuery<TId>>();

                    await selectQuery.UnlockSelectAsync(
                        outboxProcessesIds.Keys,
                        cancellationToken);
                }

                // Чтобы основной загрузчик не пытался их загрузить.
                foreach (var elem in outboxProcessesIds)
                {
                    notLoadedProcesses.Remove(elem.Key);
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
            ICollection<IProcessContainer<TId>> processes,
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
