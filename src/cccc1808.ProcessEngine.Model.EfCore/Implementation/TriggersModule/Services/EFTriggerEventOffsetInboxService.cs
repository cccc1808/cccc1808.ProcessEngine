using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Services
{
    /// <summary>
    /// Offset Inbox (облегченный) для TriggerEvent на основе смещения.
    /// Подходит только для брокеров, которые поддерживают <see cref="MessageDto.Partition"/> <see cref="MessageDto.Offset"/> (Kafka).
    /// Защищает от потери смещения 1) (падения consumer без комита), 2) consumer timeout.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class EFTriggerEventOffsetInboxService<TId>
        : ITriggerEventInboxService
    {
        private readonly IIdGenerator<TId> _idGenerator;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IEFDbContext _dbContext;

        public EFTriggerEventOffsetInboxService(
            IIdGenerator<TId> idGenerator,
            ILockQueryHintStore lockQueryHintStore,
            IEFDbContext dbContext)
        {
            _idGenerator = idGenerator;
            _lockQueryHintStore = lockQueryHintStore;
            _dbContext = dbContext;
        }        

        public async ValueTask<ITriggerEventInboxService.IContext> FilterMessagesAsync(
            Dictionary<string, List<(MessageDto Message, ITriggerEvent Event)>> groupByTriggerMessages,
            Dictionary<ITriggerEventInboxService.PartitionKey, ITriggerEventInboxService.PartitionOffset> offsetsData,
            int allMessages,
            CancellationToken cancellationToken)
        {
            Dictionary<ITriggerEventInboxService.PartitionKey, TriggerEventOffsetInboxDbEntity<TId>> offsets;
            using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
            {
                var joinCollection = _dbContext.QueryFromCollection(
                    offsetsData.Keys.Select(e => new { e.Queue, e.Partition }).ToArray()
                    );

                // TODO: это можно держать InMemory (context) и делать только update (или Attach for update) с оптимистичной блокировкой.
                offsets = await _dbContext
                    .Set<TriggerEventOffsetInboxDbEntity<TId>>()
                    .Join(
                        joinCollection,
                        e => new { Queue = e.QueueName, Partition = e.PartitionId },
                        e => e, (e1, e2) => e1
                        )
                    .ToDictionaryAsync(
                        e => new ITriggerEventInboxService.PartitionKey(e.QueueName, e.PartitionId), 
                        e => e,
                        cancellationToken);
            }

            // 1) Проверяем по агрегированным данным. Если все корректно, то этого достаточно.
            {
                var allProcessed = true;
                foreach (var elem in offsetsData.Values)
                {
                    if (!offsets.TryGetValue(elem.Key, out var offset))
                    {
                        // Нет в БД.
                        offset = new TriggerEventOffsetInboxDbEntity<TId>(
                            await _idGenerator.NextAsync(cancellationToken),
                            elem.Key.Queue,
                            elem.Key.Partition,
                            elem.MaxValue);

                        _dbContext.Set<TriggerEventOffsetInboxDbEntity<TId>>()
                            .Add(offset);

                        offsets.Add(elem.Key, offset);
                        continue;
                    }

                    if (elem.MinValue > offset.Offset)
                    {
                        // Offset корректный.

                        offset.Offset = elem.MaxValue;
                        continue;
                    }

                    {
                        // Offset некорректный.
                        allProcessed = false;
                        continue;
                    }
                }
                if (allProcessed)
                {
                    // Дальнейшая проверка не нужна.
                    return null;
                }
            }

            // 2) Есть расхождения, нужно проверить все сообщения.
            var forRemove = new List<string>(0);
            foreach (var elem in groupByTriggerMessages)
            {
                for (var i = 0; i < elem.Value.Count; i++)
                {
                    var elem2 = elem.Value[i];
                    var elem2Key = new ITriggerEventInboxService.PartitionKey(elem2.Message.Queue, elem2.Message.Partition);
                    var offset = offsets[elem2Key];

                    if (elem2.Message.Offset <= offset.Offset)
                    {
                        // Обнаружен некорректное смещение.
                        elem.Value.RemoveAt(i);
                        i--;

                        // TODO: log
                    }
                    else
                    {
                        // Обновляем offset.
                        offset.Offset = elem2.Message.Offset;
                    }
                }

                if (!elem.Value.Any())
                {
                    forRemove.Add(elem.Key);
                }
            }

            foreach (var elem in forRemove)
            {
                groupByTriggerMessages.Remove(elem);
            }

            return null;
        }

        public ValueTask AfterCommitAsync(
            ITriggerEventInboxService.IContext context,
            CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }
}
