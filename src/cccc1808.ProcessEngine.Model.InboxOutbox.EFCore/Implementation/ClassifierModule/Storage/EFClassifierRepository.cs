using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.ClassifierModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Conditions;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Entities;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.CommonModule;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Wakeup;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.ClassifierModule.Storage
{
    public class EFClassifierRepository<TId>
        : IClassifierRepository<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        /// <summary>
        /// TODO: 1) ограничение размера кеша, отчистка. 2) Генерация идентификаторов offset.
        /// </summary>
        private readonly CachState _cachState;


        public EFClassifierRepository(
            IServiceProvider serviceProvider,
            CachState cachState)
        {
            _serviceProvider = serviceProvider;
            _cachState = cachState;
        }
        
        public async ValueTask<IDictionary<(AggregateDto Aggreagate, string Queue), (TId ProcessId, TId QueueId, string Queue, string TriggerKey)>> GetInboxInfoAsync(
            ICollection<(AggregateDto Aggreagate, string Queue)> info,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<(AggregateDto Aggreagate, string Queue), (TId ProcessId, TId QueueId, string Queue, string TriggerKey)>(info.Count);
            var notFound = new List<(AggregateDto Aggreagate, string Queue)>(0);
            foreach (var elem in info)
            {
                if (_cachState._inboxInfo.TryGetValue(elem, out var id))
                {
                    result.Add(elem, id);
                }
                else
                {
                    notFound.Add(elem);
                }
            }

            if (notFound.Any())
            {
                // Можно не в основной транзакции.
                await using (var scope = _serviceProvider.CreateAsyncScope())
                {
                    var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
                    var dateTimeDbProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
                    var idGenerator = scope.ServiceProvider.GetRequiredService<IIdGenerator<TId>>();
                    var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                    var triggerRepository = scope.ServiceProvider.GetRequiredService<ITriggerRepository<TId>>();
                    var registry = scope.ServiceProvider.GetRequiredService<InboxRegistryDto>();
                    var aggregateClassifierDbEntityCondition = scope.ServiceProvider.GetRequiredService<IAggregateClassifierDbEntityCondition<TId>>();

                    var foundedQueue = new Dictionary<string, TId>(notFound.Count);
                    var foundedAggreaget = new Dictionary<AggregateDto, TId>(notFound.Count);

                    var notFoundQueue = new List<string>(notFound.Count);
                    var notFoundAggreagate = new List<AggregateDto>(notFound.Count);

                    foreach (var elem in notFound)
                    {
                        if (_cachState._queueCache.TryGetValue(elem.Queue, out var queueId))
                        {
                            foundedQueue.Add(elem.Queue, queueId);
                        }
                        else 
                        {
                            notFoundQueue.Add(elem.Queue);
                        }

                        if (_cachState._aggregateCache.TryGetValue(elem.Aggreagate, out var aggregateId))
                        {
                            foundedAggreaget.Add(elem.Aggreagate, aggregateId);
                        }
                        else 
                        {
                            notFoundAggreagate.Add(elem.Aggreagate);
                        }
                    }

                    if (notFoundQueue.Any())
                    {
                        var dbValues = await EFQueryHelper.GetOrInsertAsync<QueueClassifierDbEntity<TId>, string>(
                            dbContext,
                            keys: notFoundQueue,
                            selectQueryFunc: static (k, q) => q.Where(e => k.Contains(e.Name)),
                            keySelectorFunc: static (e) => e.Name,
                            unique: (e) => e.Name,
                            buildFunc: async (k, t) =>
                            {
                                var result = new List<QueueClassifierDbEntity<TId>>(k.Count);
                                foreach (var elem in k)
                                {
                                    result.Add(
                                        new QueueClassifierDbEntity<TId>(
                                            id: await idGenerator.NextAsync(cancellationToken),
                                            name: elem
                                            )
                                        );
                                }
                                return result;
                            },
                            cancellationToken
                            );

                            foreach (var elem in dbValues)
                            {
                                _cachState._queueCache.TryAdd(elem.Key, elem.Value.Entity.Id);
                                foundedQueue.Add(elem.Key, elem.Value.Entity.Id);
                            }
                    }

                    if (notFoundAggreagate.Any())
                    {
                        var dbValues = await EFQueryHelper.GetOrInsertAsync<AggregateClassifierDbEntity<TId>, AggregateDto>(
                            dbContext,
                            notFoundAggreagate,
                            // В2 Коррелированный подзапрос
                            // selectQueryFunc: q.ApplayQueryCondition(aggregateClassifierDbEntityCondition.AggregateDto.QueryRange, (dbContext, k)),
                            selectQueryFunc: (k, q) => 
                            {
                                _ = aggregateClassifierDbEntityCondition.AggregateDto; // для ссылки.
                                // В1.1: нормальный join
                                var collectionQuery = dbContext.QueryFromCollection(k.Select(e => new { e.AggregateId, e.AggregateType }).ToArray());
                                return q.Join(collectionQuery, e => new { e.AggregateId, e.AggregateType }, e => e, (e1, e2) => e1);
                            },
                            (e) => new AggregateDto(e.AggregateType, e.AggregateId),
                            e => new { e.AggregateId, e.AggregateType },
                            async (k, t) =>
                            {
                                var result = new List<AggregateClassifierDbEntity<TId>>(k.Count);
                                foreach (var elem in k)
                                {
                                    result.Add(
                                        new AggregateClassifierDbEntity<TId>(
                                            await idGenerator.NextAsync(t),
                                            elem.AggregateType,
                                            elem.AggregateId
                                            ));
                                }
                                return result;
                            },
                            cancellationToken
                            );

                        foreach (var elem in dbValues)
                        {
                            _cachState._aggregateCache.TryAdd(elem.Key, elem.Value.Entity.Id);
                            foundedAggreaget.Add(elem.Key, elem.Value.Entity.Id);
                        }
                    }

                    var foundedAggreagetR = foundedAggreaget.ToDictionary(e => e.Value, e => e.Key);
                    var foundedQueueR = foundedQueue.ToDictionary(e => e.Value, e => e.Key);

                    await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                    {
                        // TODO: INFO: наверное более адекватным будет добавить в ProcessDbEntity IdempotencyId (unique ProcessTypeId + IdempotencyId),
                        // тогда можно будет делать (insert if not exists) в таблицу основного процесса (и получить sequence Id для заполнения ссылок ProcessId). 
                        var dbValues = await EFQueryHelper.GetOrInsertAsync<InboxProcessDataDbEntity<TId>, (AggregateDto Aggreagate, string Queue)>(
                            dbContext,
                            notFound,
                            (k, q) =>
                            {
                                // В1.1: нормальный join
                                var queryCollection = dbContext.QueryFromCollection(
                                    k.Select(
                                        e => new
                                        {
                                            QueueId = foundedQueue[e.Queue],
                                            AggregateId = foundedAggreaget[e.Aggreagate],
                                        })
                                    .ToArray());

                                return q.Join(
                                    queryCollection, 
                                    e => new { e.QueueId, e.AggregateId }, 
                                    e => e, 
                                    (e1, e2) => e1);
                            },
                            (e) => (foundedAggreagetR[e.AggregateId], foundedQueueR[e.QueueId]),
                            unique: e => new { e.AggregateId, e.QueueId },
                            async (e, t) =>
                            {
                                var result = new List<InboxProcessDataDbEntity<TId>>(e.Count);
                                foreach (var elem in e)
                                {
                                    result.Add(
                                        new InboxProcessDataDbEntity<TId>(
                                            id: await idGenerator.NextAsync(t),
                                            // TODO: проблемный момент. Из-за того, что мы сначала втавляем ProcessData у нас пока нет processId.
                                            // Guid мы пожем сгенерировать и подставить, а вот если id генерируется на стороне БД,
                                            // То нужно будет либо запрашивать у БД, либо сначала сохранять ProcessDbEntity, а потом еще обновить InboxProcessDataDbEntity.
                                            // Не будет проблемы см. [Метка 2].
                                            processId: await idGenerator.NextAsync(t),
                                            aggregateId: foundedAggreaget[elem.Aggreagate],
                                            queueId: foundedQueue[elem.Queue],
                                            processedOffset: -1,
                                            wakeupTriggerKey: Guid.NewGuid().ToString()
                                            ));
                                }
                                return result;
                            },
                            cancellationToken
                            );

                        var inserted = dbValues
                            .Where(e => e.Value.IsInserterted)
                            .ToArray();
                        if (inserted.Any())
                        {
                            var createTriggers = new List<ITriggerRepository<TId>.CreateTriggerDto>(inserted.Length);
                            foreach (var elem in inserted)
                            {
                                dbContext.Set<ProcessDbEntity<TId>>().Add(
                                    new ProcessDbEntity<TId>(
                                        id: elem.Value.Entity.ProcessId,
                                        processTypeId: registry.Registry.ProcessType.ProcessType,
                                        processVersion: registry.Registry.ProcessType.ProcessVersion,
                                        priority: registry.Registry.Priority,
                                        DateTimeOffset.MinValue,
                                        stoppedByError: false,
                                        status: ProcessStatusEnum.WaitEvent,
                                        retryCount: null));

                                //dbContext.Set<ProcessWakeupDbEntity<TId>>().Add(
                                //    new ProcessWakeupDbEntity<TId>(
                                //        id: await idGenerator.NextAsync(cancellationToken),
                                //        processId: elem.Value.Entity.ProcessId,
                                //        isAsyncExecuting: false));

                                createTriggers.Add(
                                    ITriggerRepository<TId>.CreateTriggerDto.OffsetStreamTrigger(
                                        elem.Value.Entity.WakeupTriggerKey,
                                        DateTimeOffset.MinValue,
                                        elem.Value.Entity.ProcessId,
                                        isRangeTrigger: true,
                                        NoWakeupStreamTriggerRangeHandler<TId>.Name,
                                        priority: 0,
                                        isActivated: false,
                                        streamProcessIsWaiting: true,
                                        processedOffset: 0,
                                        lastOffset: 0
                                        )
                                    );
                            }

                            await triggerRepository.CreateTriggerRangeAsync(createTriggers, cancellationToken);

                            await dbContext.SaveChangesAsync(cancellationToken);
                            // TODO: [Метка 2] Если клбюч генерируется на стороне БД, то обновить processId в InboxProcessDataDbEntity и ProcessWakeupDbEntity.
                        }

                        await transaction.CommitAsync(cancellationToken);

                        foreach (var elem in dbValues)
                        {
                            var value = (
                                elem.Value.Entity.ProcessId,
                                elem.Value.Entity.QueueId,
                                foundedQueueR[elem.Value.Entity.QueueId],
                                elem.Value.Entity.WakeupTriggerKey
                                );
                            _cachState._inboxInfo.TryAdd(elem.Key, value);
                            result.Add(elem.Key, value);
                        }
                    }
                }
            }

            return result;
        }

        public async ValueTask<IDictionary<(AggregateDto Aggreagate, string Queue), (TId ProcessId, TId QueueId, string Queue, string TriggerKey)>> GetOutboxInfoAsync(
            ICollection<(AggregateDto Aggreagate, string Queue)> info,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<(AggregateDto Aggreagate, string Queue), (TId ProcessId, TId QueueId, string Queue, string TriggerKey)>(info.Count);
            var notFound = new List<(AggregateDto Aggreagate, string Queue)>(0);
            foreach (var elem in info)
            {
                if (_cachState._outboxInfo.TryGetValue(elem, out var id))
                {
                    result.Add(elem, id);
                }
                else
                {
                    notFound.Add(elem);
                }
            }

            if (notFound.Any())
            {
                // Можно не в основной транзакции.
                await using (var scope = _serviceProvider.CreateAsyncScope())
                {
                    var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
                    var dateTimeDbProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
                    var idGenerator = scope.ServiceProvider.GetRequiredService<IIdGenerator<TId>>();
                    var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                    var triggerRepository = scope.ServiceProvider.GetRequiredService<ITriggerRepository<TId>>();
                    var registry = scope.ServiceProvider.GetRequiredService<OutboxRegistryDto>();
                    var aggregateClassifierDbEntityCondition = scope.ServiceProvider.GetRequiredService<IAggregateClassifierDbEntityCondition<TId>>();

                    var foundedQueue = new Dictionary<string, TId>(notFound.Count);
                    var foundedAggreaget = new Dictionary<AggregateDto, TId>(notFound.Count);

                    var notFoundQueue = new List<string>(notFound.Count);
                    var notFoundAggreagate = new List<AggregateDto>(notFound.Count);

                    foreach (var elem in notFound)
                    {
                        if (_cachState._queueCache.TryGetValue(elem.Queue, out var queueId))
                        {
                            foundedQueue.Add(elem.Queue, queueId);
                        }
                        else
                        {
                            notFoundQueue.Add(elem.Queue);
                        }

                        if (_cachState._aggregateCache.TryGetValue(elem.Aggreagate, out var aggregateId))
                        {
                            foundedAggreaget.Add(elem.Aggreagate, aggregateId);
                        }
                        else
                        {
                            notFoundAggreagate.Add(elem.Aggreagate);
                        }
                    }

                    if (notFoundQueue.Any())
                    {
                        var dbValues = await EFQueryHelper.GetOrInsertAsync<QueueClassifierDbEntity<TId>, string>(
                            dbContext,
                            keys: notFoundQueue,
                            selectQueryFunc: static (k, q) => q.Where(e => k.Contains(e.Name)),
                            keySelectorFunc: static (e) => e.Name,
                            unique: e => e.Name,
                            buildFunc: async (k, t) =>
                            {
                                var result = new List<QueueClassifierDbEntity<TId>>(k.Count);
                                foreach (var elem in k)
                                {
                                    result.Add(
                                        new QueueClassifierDbEntity<TId>(
                                            id: await idGenerator.NextAsync(cancellationToken),
                                            name: elem
                                            )
                                        );
                                }
                                return result;
                            },
                            cancellationToken
                            );

                        foreach (var elem in dbValues)
                        {
                            _cachState._queueCache.TryAdd(elem.Key, elem.Value.Entity.Id);
                            foundedQueue.Add(elem.Key, elem.Value.Entity.Id);
                        }
                    }

                    if (notFoundAggreagate.Any())
                    {
                        var dbValues = await EFQueryHelper.GetOrInsertAsync<AggregateClassifierDbEntity<TId>, AggregateDto>(
                            dbContext,
                            notFoundAggreagate,
                            // В2 Коррелированный подзапрос
                            // (k, q) => q.ApplayQueryCondition(aggregateClassifierDbEntityCondition.AggregateDto.QueryRange, (dbContext, k)),
                            selectQueryFunc: (k, q) =>
                            {
                                _ = aggregateClassifierDbEntityCondition.AggregateDto; // для ссылки.
                                // В1.1: нормальный join
                                var collectionQuery = dbContext.QueryFromCollection(k.Select(e => new { e.AggregateId, e.AggregateType }).ToArray());
                                return q.Join(collectionQuery, e => new { e.AggregateId, e.AggregateType }, e => e, (e1, e2) => e1);
                            },                            
                            (e) => new AggregateDto(e.AggregateType, e.AggregateId),
                            unique: e => new { e.AggregateId, e.AggregateType },
                            async (k, t) =>
                            {
                                var result = new List<AggregateClassifierDbEntity<TId>>(k.Count);
                                foreach (var elem in k)
                                {
                                    result.Add(
                                        new AggregateClassifierDbEntity<TId>(
                                            await idGenerator.NextAsync(t),
                                            elem.AggregateType,
                                            elem.AggregateId
                                            ));
                                }
                                return result;
                            },
                            cancellationToken
                            );

                        foreach (var elem in dbValues)
                        {
                            _cachState._aggregateCache.TryAdd(elem.Key, elem.Value.Entity.Id);
                            foundedAggreaget.Add(elem.Key, elem.Value.Entity.Id);
                        }
                    }

                    var foundedAggreagetR = foundedAggreaget.ToDictionary(e => e.Value, e => e.Key);
                    var foundedQueueR = foundedQueue.ToDictionary(e => e.Value, e => e.Key);

                    await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                    {
                        var dbValues = await EFQueryHelper.GetOrInsertAsync<OutboxProcessDataDbEntity<TId>, (AggregateDto Aggreagate, string Queue)>(
                            dbContext,
                            notFound,                            
                            (k, q) =>
                            {
                                // В1.1: нормальный join
                                var queryCollection = dbContext.QueryFromCollection(
                                    k.Select(
                                        e => new
                                        {
                                            QueueId = foundedQueue[e.Queue],
                                            AggregateId = foundedAggreaget[e.Aggreagate],
                                        })
                                    .ToArray());

                                return q.Join(
                                    queryCollection,
                                    e => new { e.QueueId, e.AggregateId },
                                    e => e,
                                    (e1, e2) => e1);
                            },
                            (e) => (foundedAggreagetR[e.AggregateId], foundedQueueR[e.QueueId]),
                            unique: e => new { e.AggregateId, e.QueueId },
                            async (e, t) =>
                            {
                                var result = new List<OutboxProcessDataDbEntity<TId>>(e.Count);
                                foreach (var elem in e)
                                {
                                    result.Add(
                                        new OutboxProcessDataDbEntity<TId>(
                                            id: await idGenerator.NextAsync(t),
                                            // TODO: проблемный момент. Из-за того, что мы сначала втавляем ProcessData у нас пока нет processId.
                                            // Guid мы пожем сгенерировать и подставить, а вот если id генерируется на стороне БД,
                                            // То нужно будет либо запрашивать у БД, либо сначала сохранять ProcessDbEntity, а потом еще обновить InboxProcessDataDbEntity.
                                            processId: await idGenerator.NextAsync(t),
                                            aggregateId: foundedAggreaget[elem.Aggreagate],
                                            queueId: foundedQueue[elem.Queue],
                                            wakeupTriggerKey: Guid.NewGuid().ToString()
                                            ));
                                }
                                return result;
                            },
                            cancellationToken
                            );

                        var inserted = dbValues
                            .Where(e => e.Value.IsInserterted)
                            .ToArray();
                        if (inserted.Any())
                        {
                            var createTriggers = new List<ITriggerRepository<TId>.CreateTriggerDto>(inserted.Length);
                            foreach (var elem in inserted)
                            {
                                dbContext.Set<ProcessDbEntity<TId>>().Add(
                                    new ProcessDbEntity<TId>(
                                        id: elem.Value.Entity.ProcessId,
                                        processTypeId: registry.Registry.ProcessType.ProcessType,
                                        processVersion: registry.Registry.ProcessType.ProcessVersion,
                                        priority: registry.Registry.Priority,
                                        DateTimeOffset.MinValue,
                                        stoppedByError: false,
                                        status: ProcessStatusEnum.WaitEvent,
                                        retryCount: null));

                                //dbContext.Set<ProcessWakeupDbEntity<TId>>().Add(
                                //    new ProcessWakeupDbEntity<TId>(
                                //        id: await idGenerator.NextAsync(cancellationToken),
                                //        processId: elem.Value.Entity.ProcessId,
                                //        isAsyncExecuting: false));

                                createTriggers.Add(
                                    ITriggerRepository<TId>.CreateTriggerDto.SimpleStreamTrigger(
                                        elem.Value.Entity.WakeupTriggerKey,
                                        DateTimeOffset.MinValue,
                                        elem.Value.Entity.ProcessId,
                                        isRangeTrigger: true,
                                        EFOutboxTriggerWakeupHandler<TId>.Name,
                                        priority: 0,
                                        isActivated: false,
                                        streamProcessIsWaiting: true,
                                        newSignalCounter: 0,
                                        isRootTrigger: false)
                                    );
                            }

                            await triggerRepository.CreateTriggerRangeAsync(createTriggers, cancellationToken);

                            await dbContext.SaveChangesAsync(cancellationToken);
                            // TODO: Если клбюч генерируется на стороне БД, то обновить processId в InboxProcessDataDbEntity и ProcessWakeupDbEntity.
                        }

                        await transaction.CommitAsync(cancellationToken);

                        foreach (var elem in dbValues)
                        {
                            var value = (
                                elem.Value.Entity.ProcessId,
                                elem.Value.Entity.QueueId,
                                foundedQueueR[elem.Value.Entity.QueueId],
                                elem.Value.Entity.WakeupTriggerKey
                                );
                            _cachState._outboxInfo.TryAdd(elem.Key, value);
                            result.Add(elem.Key, value);
                        }
                    }
                }
            }

            return result;
        }

        public ValueTask<long> GetOutboxOrderIdAsync((AggregateDto Aggreagate, string Queue) aggregate, CancellationToken cancellationToken)
        {
            var result = _cachState._inboxOffset.AddOrUpdate(
                aggregate, 
                0,
                static (k, e) => e + 1);
            return ValueTask.FromResult(result);
        }

        public ValueTask<long> GetInboxOrderIdAsync((AggregateDto Aggreagate, string Queue) aggregate, CancellationToken cancellationToken)
        {
            var result = _cachState._outboxOffset.AddOrUpdate(
                aggregate,
                0,
                static (k, e) => e + 1);
            return ValueTask.FromResult(result);
        }

        public class CachState 
        {
            public readonly ConcurrentDictionary<string, TId> _queueCache;
            public readonly ConcurrentDictionary<AggregateDto, TId> _aggregateCache;
            public readonly ConcurrentDictionary<(AggregateDto Aggreagate, string Queue), (TId ProcessId, TId QueueId, string Queue, string TriggerKey)> _inboxInfo;
            public readonly ConcurrentDictionary<(AggregateDto Aggreagate, string Queue), (TId ProcessId, TId QueueId, string Queue, string TriggerKey)> _outboxInfo;

            // TODO: переделать
            public readonly ConcurrentDictionary<(AggregateDto Aggreagate, string Queue), long> _inboxOffset;
            public readonly ConcurrentDictionary<(AggregateDto Aggreagate, string Queue), long> _outboxOffset;

            public CachState()
            {
                _queueCache = new ConcurrentDictionary<string, TId>();
                _aggregateCache = new ConcurrentDictionary<AggregateDto, TId>();
                _inboxInfo = new ConcurrentDictionary<(AggregateDto Aggreagate, string Queue), (TId ProcessId, TId QueueId, string Queue, string TriggerKey)>();
                _outboxInfo = new ConcurrentDictionary<(AggregateDto Aggreagate, string Queue), (TId ProcessId, TId QueueId, string Queue, string TriggerKey)>();

                _inboxOffset = new ConcurrentDictionary<(AggregateDto Aggreagate, string Queue), long>();
                _outboxOffset = new ConcurrentDictionary<(AggregateDto Aggreagate, string Queue), long>();
            }

            public void Clear() 
            {
                _queueCache.Clear();
                _aggregateCache.Clear();
                _inboxInfo.Clear();
                _outboxInfo.Clear();
                _inboxOffset.Clear();
                _outboxOffset.Clear();
            }
        }        
    }
}
