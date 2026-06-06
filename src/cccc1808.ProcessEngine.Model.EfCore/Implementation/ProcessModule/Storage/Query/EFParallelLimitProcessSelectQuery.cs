using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Query
{
    /// <summary>
    /// Похож на <see cref="EFTriggerSelectQuery{TId}"/>.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    /// <typeparam name="TEntity"></typeparam>
    public class EFParallelLimitProcessSelectQuery<TId, TEntity> 
        : IParallelLimitProcessRunner.ISelectQuery<TId>
        where TEntity : ProcessDbEntity<TId>
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEFDbContext _dbContext;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IProcessRegistry _processRegistry;

        private readonly IProcessDbEntityConditions<TId, TEntity> _processDbEntityConditions;

        public EFParallelLimitProcessSelectQuery(
            IDateTimeProvider dateTimeProvider,
            IEFDbContext dbContext,
            ILockQueryHintStore lockQueryHintStore,
            IProcessRegistry processRegistry,
            IProcessDbEntityConditions<TId, TEntity> processDbEntityConditions)
        {
            _dateTimeProvider = dateTimeProvider;
            _dbContext = dbContext;
            _lockQueryHintStore = lockQueryHintStore;
            _processRegistry = processRegistry;

            _processDbEntityConditions = processDbEntityConditions;            
        }

        public IParallelLimitProcessRunner.ISelectQuery<TId>.IContextState BuildContext(
            IParallelLimitProcessRunner.ISelectQuery<TId>.ISelectOptions selectOptions)
        {
            return selectOptions switch
            {
                Options1 options1 => new Context1(options1),

                _ => throw new NotImplementedException(),
            };
        }

        public async Task<ICollection<IParallelLimitProcessRunner.ISelectQuery<TId>.SelectDto>> SelectForProcessingAsync(
            IParallelLimitProcessRunner.ISelectQuery<TId>.IContextState contextState,
            CancellationToken cancellationToken)
        {
            return contextState switch
            {
                Context1 context1 => await Implementation1Async(context1, true, cancellationToken),

                _ => throw new NotImplementedException(),
            };
        }

        private async Task<ICollection<IParallelLimitProcessRunner.ISelectQuery<TId>.SelectDto>> Implementation1Async(
            Context1 state,
            bool canInvoke,
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;
            var registrations = _processRegistry.All();

            if (state.IsRangePhase)
            {
                //// Фаза обработки Range
                IParallelLimitProcessRunner.ISelectQuery<TId>.SelectDto[] result;
                using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
                {
                    // В1: нормальный join
                    var registrationQuery = _dbContext.QueryFromCollection(
                        registrations
                        .Select(e => new
                        {
                            ProcessTypeId = e.ProcessType.ProcessType,
                            ProcessVersion = e.ProcessType.ProcessVersion,
                            Priority = e.Priority,
                        })
                        .ToArray());
                    var query = _dbContext.Set<TEntity>()
                        .Join(
                            registrationQuery,
                            e => new { e.ProcessTypeId, e.ProcessVersion, e.Priority },
                            e => e,
                            (e1, e2) => new { Process = e1, e2 }
                            );
                    query = query.ApplayQueryCondition(
                        _processDbEntityConditions.DbProcessingForSelectorForProjection2(query),
                        e => e.Process,
                        new IProcessDbEntityConditions<TId, TEntity>.DbProcessingForSelectorParameters2(
                            now, 
                            isRangeExecution: true, 
                            _dbContext,
                            registrations)
                        );

                    var data = await query
                        .OrderByDescending(e => e.Process.Priority)                                                
                        //.ThenBy(e => e.Process.ProcessTypeId)
                        //.ThenBy(e => e.Process.ProcessVersion)
                        //.ThenBy(e => e.Process.ReservationTimeout)
                        .Take(state.Options.RangeBatchSize(state.ParallelSlots))
                        .Select(e => new { e.Process.Id, e.Process.ProcessTypeId, e.Process.ProcessVersion, e.Process.Priority })
                        .ToArrayAsync(cancellationToken);

                    result = data
                        .Select(e => new IParallelLimitProcessRunner.ISelectQuery<TId>.SelectDto(
                            new ProcessInstanceInfoDto<TId>(
                                e.Id,
                                new ProcessTypeDto(e.ProcessTypeId, e.ProcessVersion),
                                e.Priority),
                            IsRangeProcess: true))
                        .ToArray();
                }

                if (!result.Any())
                {
                    state.IsRangePhase = false;

                    if (canInvoke)
                    {
                        return await Implementation1Async(
                            state,
                            canInvoke: false,
                            cancellationToken);
                    }

                    return [];
                }

                if (state.Options.RangeTriggerSelectLock != TimeSpan.Zero)
                {
                    await _dbContext.Set<TEntity>()
                        .Where(e => result.Select(e => e.ProcessInstanceInfo.Id).Contains(e.Id))
                        .ExecuteUpdateAsync(
                            e => e.SetProperty(
                                e => e.ReservationTimeout, 
                                _dateTimeProvider.UtcNow + state.Options.RangeTriggerSelectLock
                                ),
                        cancellationToken);
                }

                state.IsRangePhase = false;

                return result;
            }
            else
            {
                //// Фаза обработки Single.
                IParallelLimitProcessRunner.ISelectQuery<TId>.SelectDto[] result;
                using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
                {
                    var registrationQuery = _dbContext.QueryFromCollection(
                        registrations
                        .Select(e => new
                        {
                            ProcessTypeId = e.ProcessType.ProcessType,
                            ProcessVersion = e.ProcessType.ProcessVersion,
                            Priority = e.Priority,
                        })
                        .ToArray());
                    var query = _dbContext.Set<TEntity>()
                        .Join(
                            registrationQuery,
                            e => new { e.ProcessTypeId, e.ProcessVersion, e.Priority },
                            e => e,
                            (e1, e2) => new { Process = e1, e2 }
                            );
                    query = query.ApplayQueryCondition(
                        _processDbEntityConditions.DbProcessingForSelectorForProjection2(query),
                        e => e.Process,
                        new IProcessDbEntityConditions<TId, TEntity>.DbProcessingForSelectorParameters2(
                            now,
                            isRangeExecution: false,
                            _dbContext, 
                            registrations)
                        );
                    var data = await query
                        .OrderByDescending(e => e.Process.Priority)
                        //.ThenBy(e => e.Process.ProcessTypeId)
                        //.ThenBy(e => e.Process.ProcessVersion)
                        //.ThenBy(e => e.Process.ReservationTimeout)
                        .Take(state.Options.SingleBatchSize(state.ParallelSlots))
                        .Select(e => new { e.Process.Id, e.Process.ProcessTypeId, e.Process.ProcessVersion, e.Process.Priority })
                        .ToArrayAsync(cancellationToken);

                    result = data
                        .Select(e => new IParallelLimitProcessRunner.ISelectQuery<TId>.SelectDto(
                            new ProcessInstanceInfoDto<TId>(
                                e.Id,
                                new ProcessTypeDto(e.ProcessTypeId, e.ProcessVersion),
                                e.Priority),
                            IsRangeProcess: false))
                        .ToArray();
                }

                if (!result.Any())
                {
                    state.IsRangePhase = true;

                    if (canInvoke)
                    {
                        return await Implementation1Async(
                            state,
                            canInvoke: false,
                            cancellationToken);
                    }

                    return [];
                }

                if (state.Options.SingleTriggerSelectLock != TimeSpan.Zero)
                {
                    await _dbContext.Set<TEntity>()
                        .Where(e => result.Select(e => e.ProcessInstanceInfo.Id).Contains(e.Id))
                        .ExecuteUpdateAsync(
                            e => e.SetProperty(
                                e => e.ReservationTimeout, 
                                _dateTimeProvider.UtcNow + state.Options.SingleTriggerSelectLock
                                ),
                            cancellationToken);
                }

                state.IsRangePhase = true;

                return result;
            }
        }

        public class Options1 : IParallelLimitProcessRunner.ISelectQuery<TId>.ISelectOptions
        {
            public TimeSpan SingleTriggerSelectLock { get; set; } 
                = TimeSpan.FromSeconds(20);

            public TimeSpan RangeTriggerSelectLock { get; set; }
                = TimeSpan.FromMinutes(1);

            public Func<int, int> RangeBatchSize { get; set; }
                = static (e) => 100;

            public Func<int, int> SingleBatchSize { get; set; }
                = static (e) => e;
        }

        public class Context1 : IParallelLimitProcessRunner.ISelectQuery<TId>.IContextState
        {           
            public Options1 Options { get; }

            /// <summary>
            /// Выбираем одиночные или групповые процессы.
            /// </summary>
            public bool IsRangePhase { get; set; }

            public int ParallelSlots { get; set; }

            public Context1(Options1 options)
            {
                Options = options;
            }

            public void SetFreeSlots(int value)
            {
                ParallelSlots = value;
            }
        }
    }
}
