using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;

using LinqToDB;
using LinqToDB.Async;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query
{
    public class Linq2DbTriggerSelectQuery<TId> : ITriggerSelectQuery<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILinq2DbDataConnection _dataConnection;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ITriggerHandlerFactory<TId> _triggerHandlerFactory;

        private readonly ITriggerDbEntityConditions<TId> _triggerDbEntityConditions;

        public Linq2DbTriggerSelectQuery(
            IServiceProvider serviceProvider,
            ILinq2DbDataConnection dataConnection,
            IDateTimeProvider dateTimeProvider,
            ITriggerHandlerFactory<TId> triggerHandlerFactory,

            ITriggerDbEntityConditions<TId> triggerDbEntityConditions)
        {
            _serviceProvider = serviceProvider;
            _dataConnection = dataConnection;
            _dateTimeProvider = dateTimeProvider;
            _triggerHandlerFactory = triggerHandlerFactory;

            _triggerDbEntityConditions = triggerDbEntityConditions;
        }

        public ITriggerSelectQuery<TId>.IContextState BuildContext(ITriggerSelectQuery<TId>.IOptions options)
        {
            return options switch
            {
                // Options1 options1 => new State1(options1),
                // Options2 options2 => new State2(options2),
                Options3 options3 => new State3(options3),

                _ => throw new NotImplementedException(options.GetType().FullName)
            };
        }

        public async Task<ICollection<ITriggerSelectQuery<TId>.SelectDto>> SelectForProcessingAsync(
            ITriggerSelectQuery<TId>.IContextState contextState,
            CancellationToken cancellationToken)
        {
            var data = contextState switch
            {
                // State1 state1 => await Implementation1Async(state1, cancellationToken),
                // State2 state2 => await Implementation2Async(state2, cancellationToken),
                State3 state3 => await Implementation3Async(state3, canInvoke: true, cancellationToken),

                _ => throw new NotImplementedException(contextState.GetType().FullName)
            };
            return data;
        }

        public async Task<ICollection<ITriggerSelectQuery<TId>.SelectDto>> SelectForProcessingAsync(
            int batchSize,
            int parallelLimit,
            int transactionUpdateLimit,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return await Implementation2Async(
                batchSize,
                parallelLimit,
                transactionUpdateLimit,
                timeout,
                cancellationToken);
        }        

        private async Task<ICollection<ITriggerSelectQuery<TId>.SelectDto>> Implementation1Async(
            int batchSize,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;
            var selectLockValue = now + timeout;

            var data = await _dataConnection.Set<TriggerDbEntity<TId>>()
                .ApplayQueryCondition(
                    _triggerDbEntityConditions.DbProcessingForSelector2.Query,
                    new ITriggerDbEntityConditions<TId>.DbProcessingForSelectorParameters(
                        now)
                    )
                .Take(batchSize)
                .Select(e => new { e.Id, e.HandlerKey })
                .ToArrayAsync(cancellationToken);

            await _dataConnection.Set<TriggerDbEntity<TId>>()
                .Where(e => data.Select(e => e.Id).Contains(e.Id))
                .Set(e => e.SelectLockTimeout, selectLockValue)
                .UpdateAsync(cancellationToken);

            var result = data
                .Select(e => new ITriggerSelectQuery<TId>.SelectDto(e.Id, e.HandlerKey))
                .ToArray();

            return result;
        }

        private async Task<ICollection<ITriggerSelectQuery<TId>.SelectDto>> Implementation2Async(
            int batchSize,
            int parallelLimit,
            int transactionUpdateLimit,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;
            var selectLockValue = now + timeout;
            var result = new List<ITriggerSelectQuery<TId>.SelectDto>(batchSize);

            {
                var data = await _dataConnection.Set<TriggerDbEntity<TId>>()
                .ApplayQueryCondition(
                    _triggerDbEntityConditions.DbProcessingForSelector2.Query,
                    new ITriggerDbEntityConditions<TId>.DbProcessingForSelectorParameters(
                        now)
                    )
                .QueryHint(PostgresQueryHint.ForNoKeyUpdateSkipLocked)
                .Take(batchSize)
                .Select(e => new { e.Id, e.HandlerKey })
                .ToArrayAsync(cancellationToken);

                // Ограничиваем по количетсву параллельных слотов в зависимости от типа триггера.
                var parallelCounter = 0;
                var rangeTriggerGroups = new Dictionary<string, int>(parallelLimit);
                foreach (var elem in data)
                {
                    var handler = _triggerHandlerFactory.GetHandler(_serviceProvider, elem.HandlerKey);
                    var elemResult = new ITriggerSelectQuery<TId>.SelectDto(elem.Id, elem.HandlerKey);

                    switch (handler)
                    {
                        case ITriggerRangeHandler<TId> rangeTrigger:
                            {
                                // Ограничение 
                                if (rangeTriggerGroups.TryGetValue(elem.HandlerKey, out var value))
                                {
                                    result.Add(elemResult);

                                    if (value < transactionUpdateLimit)
                                    {
                                        // Общая транзакция
                                        rangeTriggerGroups[elem.HandlerKey] = value + 1;
                                    }
                                    else
                                    {
                                        // Новая транзакция (parallel slot)
                                        rangeTriggerGroups[elem.HandlerKey] = 1;
                                        parallelCounter++;
                                    }
                                }
                                else
                                {
                                    // Новая транзакция (parallel slot)
                                    result.Add(elemResult);
                                    rangeTriggerGroups.Add(elem.HandlerKey, 1);
                                    parallelCounter++;
                                }

                                break;
                            }

                        case ITriggerSingleHandler<TId> singleHandler:
                            {
                                result.Add(elemResult);
                                parallelCounter++;
                                break;
                            }
                    }

                    // Все слоты заняты.
                    if (parallelCounter == parallelLimit)
                    {
                        break;
                    }
                }
            }

            await _dataConnection.Set<TriggerDbEntity<TId>>()
                .Where(e => result.Select(e => e.Id).Contains(e.Id))
                .Set(e => e.SelectLockTimeout, selectLockValue)
                .UpdateAsync(cancellationToken);


            return result;
        }

        /// <summary>
        /// Реализация с учетом ограничения параллелизма.
        /// Приоритет на RangeTrigger.
        /// Минимизация избыточныъ блокировок (когда в выборку попадают SingleTrigger).
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task<ICollection<ITriggerSelectQuery<TId>.SelectDto>> Implementation3Async(
            State3 state,
            bool canInvoke,
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;

            if (state.IsRangePhase)
            {
                //// Фаза обработки RangeTrigger                
                ITriggerSelectQuery<TId>.SelectDto[] result;
                {
                    var data = await _dataConnection.Set<TriggerDbEntity<TId>>()
                        .ApplayQueryCondition(
                            _triggerDbEntityConditions.DbProcessingForSelector3.Query,
                            new ITriggerDbEntityConditions<TId>.DbProcessingForSelectorParameters3(
                                now,
                                IsRangeTrigger: true))
                        .Where(e => e.IsRangeHandler)
                        .QueryHint(PostgresQueryHint.ForNoKeyUpdateSkipLocked)
                        .Take(state.Options.RangeTriggerBatchSize(state.ParallelSlots))
                        .Select(e => new { e.Id, e.HandlerKey })
                        .ToArrayAsync(cancellationToken);

                    result = data
                        .Select(e => new ITriggerSelectQuery<TId>.SelectDto(e.Id, e.HandlerKey))
                        .ToArray();
                }

                if (!result.Any())
                {
                    state.PhaseCounter = 0;
                    state.IsRangePhase = false;

                    if (canInvoke)
                    {
                        return await Implementation3Async(
                            state,
                            canInvoke: false,
                            cancellationToken);
                    }

                    return [];
                }

                if (state.Options.RangeTriggerSelectLock != TimeSpan.Zero)
                {
                    await _dataConnection.Set<TriggerDbEntity<TId>>()
                        .Where(e => result.Select(e => e.Id).Contains(e.Id))
                        .Set(e => e.SelectLockTimeout, _dateTimeProvider.UtcNow + state.Options.RangeTriggerSelectLock)
                        .UpdateAsync(cancellationToken);
                }

                if (state.PhaseCounter == state.Options.StepInRangePhase)
                {
                    state.PhaseCounter = 0;
                    state.IsRangePhase = false;

                }
                else
                {
                    state.PhaseCounter++;
                }

                return result;
            }
            else
            {
                //// Фаза обработки SingleTrigger.
                ITriggerSelectQuery<TId>.SelectDto[] result;
                {
                    var data = await _dataConnection.Set<TriggerDbEntity<TId>>()
                        .ApplayQueryCondition(
                            _triggerDbEntityConditions.DbProcessingForSelector3.Query,
                            new ITriggerDbEntityConditions<TId>.DbProcessingForSelectorParameters3(
                                now,
                                IsRangeTrigger: false))
                        .QueryHint(PostgresQueryHint.ForNoKeyUpdateSkipLocked)
                        .Take(state.Options.SingleTriggerBatchSize(state.ParallelSlots))
                        .Select(e => new { e.Id, e.HandlerKey })
                        .ToArrayAsync(cancellationToken);

                    result = data
                        .Select(e => new ITriggerSelectQuery<TId>.SelectDto(e.Id, e.HandlerKey))
                        .ToArray();
                }

                if (!result.Any())
                {
                    state.PhaseCounter = 0;
                    state.IsRangePhase = true;

                    if (canInvoke)
                    {
                        return await Implementation3Async(
                            state,
                            canInvoke: false,
                            cancellationToken);
                    }

                    return [];
                }

                if (state.Options.SingleTriggerSelectLock != TimeSpan.Zero)
                {
                    await _dataConnection.Set<TriggerDbEntity<TId>>()
                        .Where(e => result.Select(e => e.Id).Contains(e.Id))
                        .Set(e => e.SelectLockTimeout, _dateTimeProvider.UtcNow + state.Options.SingleTriggerSelectLock)
                        .UpdateAsync(cancellationToken);
                }

                state.PhaseCounter = 0;
                state.IsRangePhase = true;

                return result;
            }
        }

        #region types

        public class Options3 : ITriggerSelectQuery<TId>.IOptions
        {
            public int StepInRangePhase { get; set; }
                = 9;

            public TimeSpan RangeTriggerSelectLock { get; set; }
                = TimeSpan.FromSeconds(10);

            public Func<int, int> RangeTriggerBatchSize { get; set; }
                = (freeSlots) => 100;

            public TimeSpan SingleTriggerSelectLock { get; set; }
                = TimeSpan.FromMinutes(1);

            public Func<int, int> SingleTriggerBatchSize { get; set; }
                = (freeSlots) => freeSlots > 1
                ? freeSlots / 2
                : 0;
        }

        public class State3 : ITriggerSelectQuery<TId>.IContextState
        {
            public Options3 Options { get; }

            public int ParallelSlots { get; set; }

            public bool IsRangePhase { get; set; }

            public int PhaseCounter { get; set; }

            public State3(Options3 options)
            {
                Options = options;
            }

            public void SetFreeSlots(int freeSlotsCount)
            {
                ParallelSlots = freeSlotsCount;
            }
        }

        #endregion
    }
}
