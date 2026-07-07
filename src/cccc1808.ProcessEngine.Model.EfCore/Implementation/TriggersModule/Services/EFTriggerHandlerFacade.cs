using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Services
{
    public class EFTriggerHandlerFacade<TId> : ITriggerHandlerFacade<TId>
    {
        private readonly IEFDbContext _dbContext;
        private readonly IProcessRegistry _processRegistry;
        private readonly ITriggerSetter<TId> _triggerSetter;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IWakeupService<TId> _wakeupService;

        public EFTriggerHandlerFacade(
            IEFDbContext dbContext,
            IProcessRegistry processRegistry,
            ITriggerSetter<TId> triggerSetter,
            ILockQueryHintStore lockQueryHintStore, 
            IWakeupService<TId> wakeupService)
        {
            _dbContext = dbContext;
            _processRegistry = processRegistry;
            _triggerSetter = triggerSetter;
            _lockQueryHintStore = lockQueryHintStore;
            _wakeupService = wakeupService;
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

        public async Task<ISet<TId>> ToAsyncExecutingNoWakeupAsync(
            IEnumerable<ITriggerComponent<TId>> triggers,
            CancellationToken cancellationToken)
        {
            // Предпологается, что блокировка была выше.
            var processes = await _dbContext.Set<ProcessDbEntity<TId>>()
                .Where(e => triggers.Select(e => e.ProcessId).Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

            var result = new HashSet<TId>(processes.Count);
            foreach (var elem in triggers)
            {
                var process = processes[elem.ProcessId];
                
                if (
                    elem.Kind is ITriggerComponent.TriggerKind.SimpleStreamRoot
                    && _processRegistry.UseSignalCode(new ProcessTypeDto(process.ProcessTypeId, process.ProcessVersion)))
                {
                    // Процесс выступает источником перечня списка игноирования, перезаписываем.
                    _triggerSetter.ChildTriggerSetter.SetSignalFilter(elem, new BitFlagDto(process.SignalCodeFilter));

                    if (_triggerSetter.ChildTriggerSetter.CheckSignal(elem, out var filteredSignals))
                    {
                        process.Status = ProcessStatusEnum.AsyncExecute;

                        // Отфильтрованные сигналы доставлены.
                        process.SignalCode = new BitFlagDto(process.SignalCode)
                            .AddFlag(filteredSignals)
                            .Bits;
                        _triggerSetter.ChildTriggerSetter.SetSignalCode(
                            elem,
                            elem.SignalCode.Value.RemoveFlag(filteredSignals));

                        result.Add(process.Id);
                    }
                    else 
                    {
                        // Процесс не пробуждается т.к. все имеющиеся сигналы отфильтрованы.
                    }
                }
                else 
                {
                    process.Status = ProcessStatusEnum.AsyncExecute;
                }
            }

            return result;
        }

        public async Task ToAsyncExecutingWakeupAsync(
            ICollection<TId> processIds,
            CancellationToken cancellationToken)
        {
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
                            .Where(e => e.Status == ProcessStatusEnum.WaitEvent && e.ReservationTimeout < timeout)
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
