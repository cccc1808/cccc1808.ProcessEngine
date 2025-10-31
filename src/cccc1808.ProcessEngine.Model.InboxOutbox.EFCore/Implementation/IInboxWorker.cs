using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto.Registry;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.QueueProvider;
using cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation
{
    /// <summary>
    /// Воркер чтения очередей.
    /// Queue -> Inbox.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    /// <typeparam name="TDbContext"></typeparam>
    public class IInboxWorker<TId, TDbContext>
        : IAsyncDisposable
        where TDbContext : DbContext
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IQueueProviderFactory _queueProviderFactory;
        private readonly string[] _queues;
        private readonly Func<string, TId> _idFactory;
        private readonly Func<MessageDto, string> _aggregateIdFactory;
        private readonly Func<MessageDto, string> _idempotencyIdFactory;
        private readonly int _aggregateCacheSize;

        private List<(CancellationTokenSource Token, Task Task)> _consumers;

        public IInboxWorker(
            IServiceProvider serviceProvider, 
            IQueueProviderFactory queueProviderFactory, 
            string[] queues,
            Func<string, TId> idFactory,
            Func<MessageDto, string> aggregateIdFactory,
            Func<MessageDto, string> idempotencyIdFactory,
            int aggregateCacheSize)
        {
            _serviceProvider = serviceProvider;
            _queueProviderFactory = queueProviderFactory;
            _queues = queues;
            _idFactory = idFactory;
            _aggregateIdFactory = aggregateIdFactory;
            _idempotencyIdFactory = idempotencyIdFactory;
            _aggregateCacheSize = aggregateCacheSize;
            _consumers = new List<(CancellationTokenSource Token, Task Task)>(queues.Length);
        }

        public Task StartAsync()
        {
            _consumers.Clear();
            foreach (var elem in _queues)
            {
                var token = new CancellationTokenSource();
                var task = Task.Run(
                    async () => await Body(elem, token.Token)
                    );
                _consumers.Add((token, task));
            }
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            try 
            {
                foreach (var elem in _consumers)
                {
                    elem.Token.Cancel();
                }

                await Task.WhenAll(
                    _consumers.Select(e => e.Task));
            }
            finally 
            {
                foreach (var elem in _consumers)
                {
                    elem.Token.Dispose();
                }

                _consumers.Clear();
            }
        }


        private async Task Body(
            string queueName,
            CancellationToken cancelationToken) 
        {
            {
                TId queueId;
                await using (var scope = _serviceProvider.CreateAsyncScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();

                    await dbContext.Set<QueueClassifierDbEntity<TId>>()
                        .Upsert(
                            new QueueClassifierDbEntity<TId>()
                            {
                                Id = _idFactory(queueName),
                                Name = queueName
                            }
                            )
                        .RunAsync(cancelationToken);

                    queueId = (
                        await dbContext.Set<QueueClassifierDbEntity<TId>>()
                            .AsNoTracking()
                            .FirstAsync(e => e.Name == queueName, cancelationToken))
                            .Id;
                }

                var aggregateIdCache = new Dictionary<string, TId>(_aggregateCacheSize);

                var consumer = await _queueProviderFactory.GetConsumerAsync(queueName, cancelationToken);
                while (!cancelationToken.IsCancellationRequested)
                {
                    var batch = await consumer.ConsumeBatchAsync(
                        250,
                        TimeSpan.FromSeconds(2),
                        cancelationToken);

                    if (batch.Count == 0)
                    {
                        continue;
                    }

                    await using (var scope = _serviceProvider.CreateAsyncScope())
                    {
                        var aggregates = batch
                            .Select(e => (Message: e, AggregateId: _aggregateIdFactory(e)))
                            .GroupBy(e => e.AggregateId)
                            .ToArray();

                        var inboxRegistry = scope.ServiceProvider.GetRequiredService<InboxRegistryDto>();
                        var dbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
                        var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
                        var messageStreamTechService = scope.ServiceProvider.GetRequiredService<IMessageStreamTechService<TId>>();

                        // TODO: batch query
                        foreach (var elem in aggregates)
                        {
                            if (!aggregateIdCache.TryGetValue(elem.Key, out _))
                            {
                                var inboxStreamData = await dbContext.Set<InboxProcessDataDbEntity<TId>>()
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(e => e.AggregateId == elem.Key, cancelationToken);

                                if (inboxStreamData == null)
                                {
                                    // Создаем стрим, если его нет.
                                    await using (var transaction = await transactionManager.StartTransactionAsync(cancelationToken))
                                    {
                                        var result = await dbContext.Set<InboxProcessDataDbEntity<TId>>()
                                            .Upsert(new InboxProcessDataDbEntity<TId>()
                                            {
                                                Id = _idFactory(null),
                                                AggregateId = elem.Key,
                                                QueueId = queueId,
                                            })
                                            .RunAsync(cancelationToken);

                                        inboxStreamData = await dbContext.Set<InboxProcessDataDbEntity<TId>>()
                                            .AsNoTracking()
                                            .FirstAsync(e => e.AggregateId == elem.Key, cancelationToken);
                                        aggregateIdCache.Add(elem.Key, inboxStreamData.Id);

                                        if (result == 1)
                                        {
                                            dbContext.Set<TimerProcessDbEntity<TId>>()
                                                .Add(
                                                    new TimerProcessDbEntity<TId>()
                                                    {
                                                        Id = inboxStreamData.Id,
                                                        Error = new ProcessErrorDbEntity<TId>()
                                                        {
                                                            Id = inboxStreamData.Id,
                                                            Error = null,
                                                        },
                                                        HaveErrorFlag = false,
                                                        IsProcessOrTimer = false,
                                                        LinkedProcess = null,
                                                        LinkedProcessId = default,
                                                        Priority = 0,
                                                        ProcessTypeId = inboxRegistry.ProcessType.ProcessType,
                                                        ProcessVersion = inboxRegistry.ProcessType.ProcessVersion,
                                                        ReTryCount = null,
                                                        SelectLock = DateTimeOffset.MinValue.UtcDateTime,
                                                        Status = Model.Abstract.Dto.ProcessStatusEnum.WaitEvent,
                                                        TimerDate = DateTimeOffset.MinValue.UtcDateTime,
                                                    }
                                                );

                                            await dbContext.SaveChangesAsync(cancelationToken);
                                        }

                                        await transaction.CommitAsync(cancelationToken);
                                    }
                                }
                            }
                        }

                        // Записываем сообщения и запускаем стрим.
                        await using (var transaction = await transactionManager.StartTransactionAsync(cancelationToken))
                        {
                            var forInsert = new Dictionary<string, InboxMessageDbEntity<TId>>(batch.Count);
                            foreach (var elem in aggregates.SelectMany(e => e.Select(e2 => e2)))
                            {
                                using var headersJson = JsonSerializer.SerializeToDocument(
                                    elem.Message.Headers
                                        .Select(e => new HeaderDto(e.key, e.value))
                                        .ToArray()
                                        );

                                forInsert.Add(
                                    elem.Message.Key,
                                    new InboxMessageDbEntity<TId>()
                                    {
                                        Id = _idFactory(null),
                                        Key = elem.Message.Key,
                                        StreamId = aggregateIdCache[elem.AggregateId],
                                        IdemporencyId = _idempotencyIdFactory(elem.Message),
                                        Body = elem.Message.Body,
                                        Headers = headersJson.RootElement.Clone(),
                                    }
                                    );
                            }

                            // Обработка повторяющися сообщений (IdemporencyId)
                            var result = await dbContext.Set<InboxMessageDbEntity<TId>>()
                                .UpsertRange(forInsert.Values)
                                .On(e => new { e.StreamId, e.IdemporencyId })
                                .NoUpdate()
                                .RunAndReturnAsync(cancelationToken);

                            var orderId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); // TODO: fix
                            foreach (var elem in result)
                            {
                                dbContext.Set<MessageDbEntity<TId>>().Add(
                                    new MessageDbEntity<TId>()
                                    {
                                        Id = elem.Id,
                                        IsActive = true,
                                        OrderId = orderId,
                                        Priority = 0,
                                        StreamId = elem.StreamId,
                                    });
                                orderId++;
                            }

                            await messageStreamTechService.WakeUpStreamAfterMessageInsertedIfNeedAsync(
                                aggregates.Select(e => (aggregateIdCache[e.Key], (DateTimeOffset?)null)).ToArray(),
                                cancelationToken
                                );

                            await dbContext.SaveChangesAsync(cancelationToken);
                            await transaction.CommitAsync(cancelationToken);
                        }
                    }

                    // Условно
                    if (aggregateIdCache.Count > _aggregateCacheSize)
                    {
                        aggregateIdCache.Clear();
                    }
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }
    }
}
