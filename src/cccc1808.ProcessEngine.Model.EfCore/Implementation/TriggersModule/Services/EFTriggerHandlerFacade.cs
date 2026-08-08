using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Extensions;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Services
{
    public class EFTriggerHandlerFacade<TId> : ITriggerHandlerFacade<TId>
    {
        private readonly IEFDbContext _dbContext;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IWakeupService<TId> _wakeupService;
        private readonly IProcessRegistry _processRegistry;
        private readonly IProcessQueueContext<TId> _queueContext;

        private readonly IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> _processDbEntityConditions;

        public EFTriggerHandlerFacade(
            IEFDbContext dbContext,
            ILockQueryHintStore lockQueryHintStore,
            IWakeupService<TId> wakeupService,
            IProcessRegistry processRegistry,
            IProcessQueueContext<TId> queueContext,

            IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> processDbEntityConditions)
        {
            _dbContext = dbContext;
            _lockQueryHintStore = lockQueryHintStore;
            _wakeupService = wakeupService;
            _processRegistry = processRegistry;
            _queueContext = queueContext;

            _processDbEntityConditions = processDbEntityConditions;
        }

        public async Task<ITriggerHandlerFacade<TId>.LockForWaitProcessResult> LockForWaitProcessAsync(
            IEnumerable<ITriggerComponent<TId>> triggers, 
            CancellationToken cancellationToken)
        {
            var mapping = triggers.ToDictionary(e => e.ProcessId, e => e);
            var notFoundIds = triggers.Select(e => e.ProcessId).ToHashSet();

            ITriggerComponent<TId>[] waitWithLock;
            using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                var ids = await _dbContext.Set<ProcessDbEntity<TId>>()
                    .Where(e => e.Status == Model.Abstract.ProcessModule.Dto.ProcessStatusEnum.WaitEvent && notFoundIds.Contains(e.Id))
                    .Select(e => e.Id)
                    .ToArrayAsync(cancellationToken);

                waitWithLock = ids
                    .Select(e => mapping[e])
                    .ToArray();

                foreach (var elem in ids)
                {
                    notFoundIds.Remove(elem);
                }                

                if (!notFoundIds.Any())
                {
                    return new ITriggerHandlerFacade<TId>.LockForWaitProcessResult(
                        waitWithLock,
                        [],
                        [],
                        [],
                        []
                        );
                }                
            }

            {
                // TODO: Тут можно читать отдельно по индексам, но пока так.
                // TODO: MVCC concurrency (no wait).
                var ids = await _dbContext.Set<ProcessDbEntity<TId>>()
                    .Where(e => notFoundIds.Contains(e.Id))
                    .Select(e => new { e.Id, e.Status })
                    .ToArrayAsync(cancellationToken);

                var waitWithoutLock = new List<ITriggerComponent<TId>>(notFoundIds.Count);
                var isAsyncExecuting = new List<ITriggerComponent<TId>>(notFoundIds.Count);
                var inComplete = new List<ITriggerComponent<TId>>(notFoundIds.Count);                

                foreach (var elem in ids)
                {
                    switch (elem.Status)
                    {
                        case ProcessStatusEnum.WaitEvent:
                            {
                                waitWithoutLock.Add(mapping[elem.Id]);
                                break;
                            }

                        case ProcessStatusEnum.AsyncExecute:
                            {
                                isAsyncExecuting.Add(mapping[elem.Id]);
                                break;
                            }

                        case ProcessStatusEnum.Complete:
                            {
                                inComplete.Add(mapping[elem.Id]);
                                break;
                            }
                    }

                    notFoundIds.Remove(elem.Id);
                }

                var notFound = notFoundIds
                    .Select(e => mapping[e])
                    .ToArray();

                return new ITriggerHandlerFacade<TId>.LockForWaitProcessResult(
                    waitWithLock,
                    waitWithoutLock,
                    isAsyncExecuting,
                    inComplete,
                    notFound
                    );
            }
        }

        public async Task<ITriggerHandlerFacade<TId>.CheckCompleteOrNotFoundResult> CheckCompleteOrNotFound(
            IEnumerable<ITriggerComponent<TId>> triggers, 
            CancellationToken cancellationToken)
        {
            var mapping = triggers.ToDictionary(e => e.ProcessId, e => e);
            var notFoundIds = triggers.Select(e => e.ProcessId).ToHashSet();

            {
                var data = await _dbContext
                    .Set<ProcessDbEntity<TId>>()
                    .Where(e => notFoundIds.Contains(e.Id))
                    .Select(e => new { e.Id, e.Status })
                    .ToArrayAsync(cancellationToken);

                var inComplete = new List<ITriggerComponent<TId>>(data.Length);                
                var other = new List<ITriggerComponent<TId>>(data.Length);

                foreach (var elem in data)
                {
                    if (elem.Status is ProcessStatusEnum.Complete)
                    {
                        inComplete.Add(mapping[elem.Id]);
                    }
                    else
                    {
                        other.Add(mapping[elem.Id]);
                    }
                    
                    notFoundIds.Remove(elem.Id);
                }

                var notFound = notFoundIds.Select(e => mapping[e])
                    .ToArray();

                return new ITriggerHandlerFacade<TId>.CheckCompleteOrNotFoundResult(
                    inComplete,
                    notFound, 
                    other);
            }
        }

        public async Task ToAsyncExecutingNoWakeupAsync(
            ICollection<TId> processIds,
            CancellationToken cancellationToken)
        {
            ProcessDbEntity<TId>[] processWithLock;
            //using (var scope = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                // Блокировка уже есть выше. Возможно поправить параметры, чтобы не загружать 2 раз.
                processWithLock = await _dbContext.Set<ProcessDbEntity<TId>>()
                    .ApplayQueryCondition(_processDbEntityConditions.WaitEvent.Query)
                    .Where(e => processIds.Contains(e.Id))
                    .ToArrayAsync(cancellationToken);
            }
            if (!processWithLock.Any())
            {
                return;
            }

            _queueContext.IncreseBufferCapacity(processWithLock.Length);
            var firstKey = _processRegistry.Get(
                processWithLock.First().ToProcessTypeUnique<TId, ProcessDbEntity<TId>>());

            // TODO: options.
            // TODO: Возможно нужен будет разный timeout, но пока так.
            if (firstKey.Metadata.IsSignleExecuteProcess)
            {
                _queueContext.SetReserveTimeout(
                    TimeSpan.FromSeconds(30));
            }
            else 
            {
                _queueContext.SetReserveTimeout(
                    TimeSpan.FromSeconds(60));
            }

            foreach (var elem in processWithLock)
            {
                elem.Status = ProcessStatusEnum.AsyncExecute;
                _queueContext.ProcessToExecute(
                    IProcessQueueContext<TId>.ProcessDto.ProcessToExecute(
                        elem.Id,
                        _processRegistry.Get(
                            elem.ToProcessTypeUnique<TId, ProcessDbEntity<TId>>())
                        )
                    );
            }

            //await _dbContext.Set<ProcessDbEntity<TId>>()
            //    .Where(e => processIds.Contains(e.Id) && e.Status == ProcessStatusEnum.WaitEvent)
            //    .ExecuteUpdateAsync(
            //        e => e.SetProperty(e => e.Status, ProcessStatusEnum.AsyncExecute),
            //        cancellationToken);
        }

        public async Task ToAsyncExecutingWakeupAsync(
            ICollection<TId> processIds,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Предпологается удаление.");

            await _wakeupService.WakeupProcessHandlerAsync(
                processIds, 
                useShareLock: true,
                cancellationToken);
        }

        public async Task<bool> CustomEmergencyTriggerHandlerAsync(
            IServiceProvider serviceProvider,
            DateTimeOffset softTimeout,
            DateTimeOffset timeout,
            int batchSize,
            Func<IQueryable<ProcessDbEntity<TId>>, IEFDbContext, IQueryable<ProcessDbEntity<TId>>> queryFactory,
            CancellationToken cancellationToken
            )
        {
            var dt = serviceProvider.GetRequiredService<IDateTimeProvider>();

            while (true)
            {
                await using (var scope = serviceProvider.CreateAsyncScope())
                {
                    var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
                    var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();

                    await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                    {
                        var query = queryFactory(dbContext.Set<ProcessDbEntity<TId>>(), dbContext);

                        var data = await query
                            .Where(e => e.Status == ProcessStatusEnum.WaitEvent && e.LastAsyncExecuteDate < timeout)
                            .Take(batchSize)
                            .Select(e => e.Id)
                            .ToArrayAsync(cancellationToken);

                        if (!data.Any())
                        {
                            break;
                        }

                        await dbContext.Set<ProcessDbEntity<TId>>()
                            .Where(e => data.Contains(e.Id) && e.Status == ProcessStatusEnum.WaitEvent)
                            .ExecuteUpdateAsync(e => e.SetProperty(e => e.Status, ProcessStatusEnum.AsyncExecute), cancellationToken);

                        await transaction.CommitAsync(cancellationToken);
                    }
                }

                if (dt.UtcNow > softTimeout)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
