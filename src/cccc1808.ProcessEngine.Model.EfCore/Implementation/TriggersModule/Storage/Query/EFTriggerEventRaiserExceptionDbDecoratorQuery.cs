using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services.Events;

using Microsoft.EntityFrameworkCore;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query
{
    /// <summary>
    /// Для обработки ситуации, когда прямая очередь <see cref="ITriggerEvent"/> не доступна.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class EFTriggerEventRaiserExceptionDbDecoratorQuery<TId>
        : TriggerEventRaiserExceptionDbDecorator<TId>.IQuery
    {
        private readonly IIdGenerator<TId> _idGenerator;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEFDbContext _dbContext;
        private readonly IEventJsonSerializer _eventJsonSerializer;

        public EFTriggerEventRaiserExceptionDbDecoratorQuery(
            IIdGenerator<TId> idGenerator,
            ILockQueryHintStore lockQueryHintStore,
            IDateTimeProvider dateTimeProvider,
            IEFDbContext dbContext,
            IEventJsonSerializer eventJsonSerializer)
        {
            _idGenerator = idGenerator;
            _lockQueryHintStore = lockQueryHintStore;
            _dateTimeProvider = dateTimeProvider;
            _dbContext = dbContext;
            _eventJsonSerializer = eventJsonSerializer;
        }

        public async Task SaveToDbOutboxAsync(
            ICollection<ITriggerEventRaiser<TId>.RaiseContainer> events, 
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;
            var ids = await _idGenerator.NextRangeAsync(events.Count, cancellationToken);

            _dbContext.Set<TriggerEventOutboxDbEntity<TId>>()
                .AddRange(
                    events.Select(
                        (e, i) => new TriggerEventOutboxDbEntity<TId>(
                            id: ids.Dequeue(),
                            timestamp: now.Ticks,
                            batchOrderId: (short)i,
                            data: JsonHelper.ToJsonElement(Map(e))
                            )
                    )
                );
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<ICollection<ITriggerEventRaiser<TId>.RaiseContainer>> LoadForSendAsync(
            int batchSize, 
            CancellationToken cancellationToken)
        {
            // Ждем блокировку (без параллелизма) для сохранения упорядоченности.
            // Также это не освной механизм отправки, а страхующий, поэтому пока не прорабатываю распараллеливание по нодам.

            TriggerEventOutboxDbEntity<TId>[] eventsEntities;
            using (var scope = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
            {
                eventsEntities = await _dbContext.Set<TriggerEventOutboxDbEntity<TId>>()
                    .AsNoTracking()
                    .OrderBy(e => e.Timestamp)
                    .ThenBy(e => e.BatchOrderId)
                    .Take(batchSize)
                    .ToArrayAsync(cancellationToken);
            }

            await _dbContext.Set<TriggerEventOutboxDbEntity<TId>>()
                .Where(e => eventsEntities.Select(e => e.Id).Contains(e.Id))
                .ExecuteDeleteAsync(cancellationToken);
            //_dbContext.Set<TriggerEventOutboxDbEntity<TId>>()
            //    .RemoveRange(eventsEntities);
            // await _dbContext.SaveChangesAsync(cancellationToken);

            return eventsEntities
                .Select(
                    e => Map(
                        e.Data.Deserialize<TriggerEventOutboxDbEntity<TId>.EventDto>()
                        )
                    )
                .ToArray();
        }

        private ITriggerEventRaiser<TId>.RaiseContainer Map(
            TriggerEventOutboxDbEntity<TId>.EventDto source)
        {
            return new ITriggerEventRaiser<TId>.RaiseContainer(
                EventQueue: source.EventQueue,
                ProcessId: source.ProcessId,
                Event: _eventJsonSerializer.Deserialize(source.Event)
                );
        }

        private TriggerEventOutboxDbEntity<TId>.EventDto Map(
            ITriggerEventRaiser<TId>.RaiseContainer source)
        {
            return new TriggerEventOutboxDbEntity<TId>.EventDto(
                EventQueue: source.EventQueue,
                ProcessId: source.ProcessId,
                Event: _eventJsonSerializer.Serialize(source.Event));
        }
    }
}
