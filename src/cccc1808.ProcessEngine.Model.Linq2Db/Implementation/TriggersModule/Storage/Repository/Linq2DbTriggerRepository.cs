using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;

using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.TriggersModule.Storage.Repository
{
    public class Linq2DbTriggerRepository<TId> : ITriggerRepository<TId>
    {
        private readonly ILinq2DbDataConnection _dataConnection;
        private readonly IIdGenerator<TId> _idGenerator;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ITriggerSetter<TId> _triggerSetter;

        private readonly ITriggerDbEntityConditions<TId> _triggerDbEntityConditions;

        private ITable<TriggerDbEntity<TId>> Set => _dataConnection.Set<TriggerDbEntity<TId>>();

        public Linq2DbTriggerRepository(
            ILinq2DbDataConnection dataConnection,
            IIdGenerator<TId> idGenerator,
            IDateTimeProvider dateTimeProvider,
            ITriggerSetter<TId> triggerSetter,

            ITriggerDbEntityConditions<TId> triggerDbEntityConditions)
        {
            _dataConnection = dataConnection;
            _idGenerator = idGenerator;
            _dateTimeProvider = dateTimeProvider;
            _triggerSetter = triggerSetter;
            _triggerDbEntityConditions = triggerDbEntityConditions;
        }

        public async Task<IDictionary<string, ITriggerComponent<TId>>> LoadTriggerForQueueConsumerAsync(
            ICollection<string> keys,
            CancellationToken cancellationToken)
        {
            var data = await Set
                .QueryHint(PostgresQueryHint.ForNoKeyUpdate)
                .ApplayQueryCondition(_triggerDbEntityConditions.KeyAndNotComplete.QueryRange, keys)
                .ToArrayAsync(cancellationToken);

            return data.ToDictionary(
                e => e.Key, 
                e => (ITriggerComponent<TId>)Map(_triggerSetter, e));
        }

        public async Task<ICollection<ITriggerComponent<TId>>> LoadForHandlerAsync(
            ICollection<TId> ids,
            TimeSpan waitLockTimeout,
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;
            var result = await TimeoutHelper.ExecuteWithTimeoutAsync(
                (This: this, ids, now),
                waitLockTimeout,
                static async (p, cancellationToken) =>
                {
                    var data = await p.This.Set
                        .ApplayQueryCondition(
                            p.This._triggerDbEntityConditions.DbProcessingForHandler.Query,
                            new ITriggerDbEntityConditions<TId>.DbProcessingForHandlerParameters(
                                p.now,
                                p.ids)
                            )
                        .QueryHint(PostgresQueryHint.ForNoKeyUpdate)
                        .ToArrayAsync(cancellationToken);

                    return data
                        .Select(e => (ITriggerComponent<TId>)Map(p.This._triggerSetter, e))
                        .ToArray();

                },
                cancellationToken
                );

            // Все блокировки получены.
            if (!result.IsTimeout)
            {
                return result.Result;
            }

            {
                var data = await Set
                    .ApplayQueryCondition(
                        _triggerDbEntityConditions.DbProcessingForHandler.Query,
                        new ITriggerDbEntityConditions<TId>.DbProcessingForHandlerParameters(
                            now,
                            ids)
                        ) // Для индекса.
                    .Where(e => ids.Contains(e.Id))
                    .QueryHint(PostgresQueryHint.ForNoKeyUpdateSkipLocked)
                    .ToArrayAsync(cancellationToken);

                return data
                    .Select(e => (ITriggerComponent<TId>)Map(_triggerSetter, e))
                    .ToArray();
            }
        }

        public async Task CreateTriggerAsync(
            ITriggerRepository<TId>.CreateTriggerDto createDto,
            CancellationToken cancellationToken)
        {
            await CreateTriggerRangeAsync(
                [createDto],
                cancellationToken);
        }

        public async Task CreateTriggerRangeAsync(
            ICollection<ITriggerRepository<TId>.CreateTriggerDto> createDto,
            CancellationToken cancellationToken)
        {
            var create = new List<TriggerDbEntity<TId>>(createDto.Count);
            foreach (var elem in createDto)
            {
                if (elem.key.Length > 255)
                {
                    throw new ArgumentException(nameof(elem.key));
                }
                if (elem.handlerKey.Length > 255)
                {
                    throw new ArgumentException(nameof(elem.handlerKey));
                }

                create.Add(new TriggerDbEntity<TId>(
                    id: await _idGenerator.NextAsync(cancellationToken),
                    key: elem.key,
                    selectLockTimeout: DateTimeOffset.MinValue,
                    timerDate: elem.timerDate,
                    handlerKey: elem.handlerKey,
                    kind: elem.kind,
                    priority: elem.priority,
                    isActivated: elem.isActivated,
                    isCompleted: false,
                    processId: elem.processId,
                    streamProcessIsWaiting: elem.streamProcessIsWaiting,
                    signalCounter1: elem.signalCounter1,
                    signalCounter2: elem.signalCounter2
                    ));
            }

            await Set.BulkCopyAsync(create, cancellationToken);
        }

        public async Task SaveAsync(
            ICollection<ITriggerComponent<TId>> triggers,
            CancellationToken cancellationToken)
        {
            var forUpdate = new List<TriggerDbEntity<TId>>(triggers.Count);
            var forRemove = new List<string>(triggers.Count);
            foreach (var elem in triggers)
            {
                if (elem.NeedRemove)
                {
                    forRemove.Add(elem.Key);
                }
                else if (elem.NeedUpdate)
                {
                    var state = _triggerSetter.OneOfSetter.OneOfTrigger(
                        elem,
                        true,
                        counterHandler: static (state, r) =>
                        {
                            return (StreamsProcessIsWaiting: (bool?)null, signalCounter1: state.Counter, signalCounter2: (long?)null);
                        },
                        timerHandler: static (_) => ((bool?)null, (long?)null, (long?)null),
                        simpleStreamHandler: static (state, r) =>
                        {
                            return (state.StreamsProcessIsWaiting, state.NewSignalCounter, (long?)null);
                        },
                        offsetStreamHanler: static (state, r) =>
                        {
                             return (state.StreamsProcessIsWaiting, state.ProcessedOffset, state.LastOffset);
                        });

                    var result = new TriggerDbEntity<TId>(
                        default,
                        elem.Key,
                        elem.SelectLockTimeout,
                        elem.TimerDate,
                        elem.HandlerKey,
                        elem.Kind,
                        default, // Не обновляется в запросе.
                        elem.IsActivated,
                        elem.IsCompleted,
                        elem.ProcessId,
                        state.Item1,
                        state.Item2,
                        state.Item3);
                    forUpdate.Add(result);
                }

            }
            
            if (forUpdate.Any())
            {
                await Set.Merge()
                    .Using(forUpdate)
                    .On((e1, e2) => e1.Key == e2.Key)
                    .UpdateWhenMatched()
                    .MergeAsync(cancellationToken);

            }
            if (forRemove.Any())
            {
                await Set
                    .Where(e => forRemove.Contains(e.Key))
                    .DeleteAsync(cancellationToken);
            }
        }        

        private static TriggerComponent<TId> Map(
            ITriggerSetter<TId> triggerSetter, 
            TriggerDbEntity<TId> source)
        {
            var state = LinkContainer.Create<object>(null);
            triggerSetter.OneOfSetter.OneOfTriggerKind(
                    source.Kind,
                    (trigger: source, state),
                    counterHandler: static p => p.state.Data = new TriggerComponent<TId>.CounterDto(
                        p.trigger.SignalCounter1.Value),
                    timerHandler: static e => { },
                    simpleStreamHandler: static p => p.state.Data = new TriggerComponent<TId>.SimpleStreamDto(
                        p.trigger.StreamProcessIsWaiting.Value,
                        p.trigger.SignalCounter1.Value),
                    offsetStreamHanler: static p => p.state.Data = new TriggerComponent<TId>.OffsetStreamDto(
                        p.trigger.StreamProcessIsWaiting.Value,
                        p.trigger.SignalCounter1.Value,
                        p.trigger.SignalCounter2.Value)
                    );

            return new TriggerComponent<TId>(
                source.Key,
                source.Kind,
                source.ProcessId,
                source.IsActivated,
                source.IsCompleted,
                source.TimerDate,
                source.HandlerKey,
                source.SelectLockTimeout,
                state.Data
                );
        }
    }
}
