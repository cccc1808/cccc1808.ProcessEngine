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
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Components;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Storage
{
    public class EFInboxDbProvider<TId>
        : IProcessDbProvider<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEFDbContext _dbContext;
        private readonly InboxRegistryDto _inboxRegistryDto;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly Options _options;
        private readonly EFChangeTrackerProcessRepository<TId, ProcessDbEntity<TId>>.Options _repositoryOptions;

        private readonly IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> _processDbEntityConditions;
        private readonly IProcessLinkedConditions<TId, InboxProcessDataDbEntity<TId>> _processLinkedConditions;
        private readonly IMessageStreamConditions<TId, InboxMessageDbEntity<TId>> _messageStreamConditions;

        public EFInboxDbProvider(
            IServiceProvider serviceProvider,
            IEFDbContext dbContext,
            InboxRegistryDto inboxRegistryDto,
            ILockQueryHintStore lockQueryHintStore,
            Options options,
            EFChangeTrackerProcessRepository<TId, ProcessDbEntity<TId>>.Options repositoryOptions,

            IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> processDbEntityConditions,
            IProcessLinkedConditions<TId, InboxProcessDataDbEntity<TId>> processLinkedConditions,
            IMessageStreamConditions<TId, InboxMessageDbEntity<TId>> messageStreamConditions
            )
        {
            _serviceProvider = serviceProvider;
            _dbContext = dbContext;
            _inboxRegistryDto = inboxRegistryDto;
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
            var inboxProcesses = byTypeIndex[_inboxRegistryDto.Registry.ProcessType];

            // 1) Загружаем данные процесса.
            var inboxData = await _dbContext.Set<InboxProcessDataDbEntity<TId>>()
                .Include(e => e.Queue)
                .Include(e => e.Aggregate)
                .ApplayQueryCondition(_processLinkedConditions.ProcessId.QueryRange, inboxProcesses)
                .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

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

            foreach (var elem in inboxProcesses)
            {
                var process = processes[elem];

                var component = new EFInboxComponentProxy<TId>(
                    inboxData[process.Id],
                    [] // messagesByStream[process.Id].Select(e => (IInboxMessageComponent<TId>)new EFInboxMessageProxy<TId>(e)).ToArray()
                    );
                process.AddComponent(component);
            }            

            // 4) В других loader могут загружаться сущности по агрегатам (Range), или загружать уже в самом хендлеое (Single).
        }

        public async Task LoadProcessForAsyncProcessingAsync(
            IDictionary<TId, ProcessInstanceInfoDto<TId>> notLoadedProcesses,
            IDictionary<TId, IProcessContainer<TId>> loadBuffer,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            var inboxProcessesIds = byTypeIndex[_inboxRegistryDto.Registry.ProcessType]
                .Select(e => notLoadedProcesses[e])
                .ToDictionary(e => e.Id, e => e);

            if (!inboxProcessesIds.Any())
            {
                return;
            }

            var softTimeout = _repositoryOptions.SoftTimeout.HasValue
                ? DateTimeOffset.Now + _repositoryOptions.SoftTimeout.Value
                : (DateTimeOffset?)null;

            (ProcessDbEntity<TId> Process, IEnumerable<InboxMessageDbEntity<TId>> Messages)[] processGroups;
            using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                var limit = _options.MessageLimitFunc(inboxProcessesIds.Count);

                var processQuery = _dbContext.Set<ProcessDbEntity<TId>>()
                    .ApplayQueryCondition(
                        _processDbEntityConditions.DbProcessingForHandler.Query,
                        new IProcessDbEntityConditions<TId, ProcessDbEntity<TId>>.DbProcessingForSelectorHandlerParameters(
                            _dbContext,
                            [_inboxRegistryDto.Registry],
                            notLoadedProcesses.Keys));
                var messagesQuery = _dbContext.Set<InboxMessageDbEntity<TId>>()
                    .ApplayQueryCondition(
                        _messageStreamConditions.ForProcessing.Query,
                        new IMessageStreamConditions<TId, InboxMessageDbEntity<TId>>.ForProcessingParamDto1(
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

            var processData = await _dbContext.Set<InboxProcessDataDbEntity<TId>>()
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
                var component = new EFInboxComponentProxy<TId>(
                    processData[elem.Process.Id],
                    elem.Messages
                        .Select(e => (IInboxMessageComponent<TId>)new EFInboxMessageProxy<TId>(e))
                        .ToArray()
                    );
                container.AddComponent<IInboxComponent<TId>>(component);

                loadBuffer.Add(container.Id, container);

                notLoadedProcesses.Remove(elem.Process.Id);
                inboxProcessesIds.Remove(elem.Process.Id);
            }


            if (inboxProcessesIds.Any())
            {
                // Процессы попали в селектор, но не уложились в лимит сообщений.

                // Снимаем select lock, чтобы другая нода могла брать их в обработку.
                // В отдельной транзакции потому, что нужно сделать сейчас, а не в конце основной транзакции.
                await using (var scope = _serviceProvider.CreateAsyncScope())
                {
                    var selectQuery = scope.ServiceProvider.GetRequiredService<IProcessAsyncProcessingSelectQuery<TId>>();

                    await selectQuery.UnlockSelectAsync(
                        inboxProcessesIds.Keys,
                        cancellationToken);
                }

                // Чтобы основной загрузчик не пытался их загрузить.
                foreach (var elem in inboxProcessesIds)
                {
                    notLoadedProcesses.Remove(elem.Key);
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
