using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Extensions;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;
using cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.T2;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.T2
{
    public class RedisTriggerQueueProvider<TId>
        : ITriggerQueueProvider<TId>
    {
        private readonly IRedisConnectionFactory _redisConnectionFactory;
        private readonly IRedisNotifyTriggerQueueState _state;

        private readonly RedisTriggerQueueOptionsDto<TId> _options;

        public RedisTriggerQueueProvider(
            IRedisConnectionFactory redisConnectionFactory,
            IRedisNotifyTriggerQueueState state,

            RedisTriggerQueueOptionsDto<TId> options)
        {
            _redisConnectionFactory = redisConnectionFactory;
            _state = state;

            _options = options;
        }

        public async Task<List<ITriggerQueueProvider<TId>.MessageDto>> ConsumeRangeTriggersAsync(
            int batchLimit,
            int uniqueHandlersLimit,
            TimeSpan timeout,            
            CancellationToken cancellationToken)
        {
            return await InnerConsumeAsync(_state.RangeTriggerState, batchLimit, uniqueHandlersLimit, timeout, cancellationToken);
        }

        public async Task<List<ITriggerQueueProvider<TId>.MessageDto>> ConsumeSignleTriggersAsync(
            int batchLimit,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return await InnerConsumeAsync(_state.SignleTriggerState, batchLimit, null, timeout, cancellationToken);
        }

        public async Task<HashSet<TId>> ProduceTriggersAsync(
            ICollection<ITriggerQueueProvider<TId>.MessageContainer> messages,
            CancellationToken cancellationToken)
        {
            return await InnerProduceAsync(messages, checkLimit: true, cancellationToken);
        }

        private async Task<HashSet<TId>> InnerProduceAsync(
            ICollection<ITriggerQueueProvider<TId>.MessageContainer> messages,
            bool checkLimit,
            CancellationToken cancellationToken)
        {
            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DbId);

            var notSended = new HashSet<TId>(0);

            var groups = messages.GroupBy(e => new IRedisNotifyTriggerQueueState.KeyDto(e.Message.HandlerKey, 0))
                .ToDictionary(e => e.Key, e => e.ToArray());

            // 1) Проверка свободного места (не строгая).
            var pipline = new List<Task>(groups.Count * 2);
            {
                if (checkLimit)
                {
                    var lenghtTasks = new Dictionary<IRedisNotifyTriggerQueueState.KeyDto, Task<long>>(groups.Count);
                    foreach (var elem in groups)
                    {
                        var t = db.SortedSetLengthAsync(_options.HandlerToQueueSetNameFactory(elem.Key));

                        pipline.Add(t);
                        lenghtTasks.Add(elem.Key, t);
                    }
                    await connection.WaitPiplineWithTimeoutAsync(pipline, cancellationToken);
                    foreach (var elem in lenghtTasks)
                    {
                        if (elem.Value.Result >= _options.QueueSizeLimit)
                        {
                            // Очередь заполнена.
                            var notSendGroup = groups[elem.Key];
                            notSended.AddRange(notSendGroup, static (m) => m.Message.TriggerId);
                            groups.Remove(elem.Key);
                        }
                    }
                }

                pipline.Clear();
            }

            // 2) Отправка.
            {
                var publishContainer = connection.GetChannelPublisher();
                foreach (var elem in groups)
                {
                    // Публикуем сообщения.
                    var t1 = db.SortedSetUpdateAsync(
                        _options.HandlerToQueueSetNameFactory(elem.Key),
                        // TODO: score можно указывать на основе LastProcessedDate timestamp (чтобы элементы размещались в пордяке даты последней обработки).
                        elem.Value
                            .Select(e => new SortedSetEntry(_options.IdToString(e.Message.TriggerId), score: -1))
                            .ToArray(),
                        when: SortedSetWhen.NotExists);

                    // Публикуем оповещения.
                    var t2 = publishContainer.PubAsync(_options.QueueChannelNameFactory(elem.Key), RedisValue.EmptyString);

                    pipline.Add(t1);
                    pipline.Add(t2);
                }
                await connection.WaitPiplineWithTimeoutAsync(pipline, cancellationToken);
            }

            return notSended;
        }

        private async Task<List<ITriggerQueueProvider<TId>.MessageDto>> InnerConsumeAsync(
            IRedisNotifyTriggerQueueState.IHandler state,
            int batchSize,
            int? uniqueHandlersLimit,
            TimeSpan batchTimeout,
            CancellationToken cancellationToken)
        {
            var buffer = new List<ITriggerQueueProvider<TId>.MessageDto>(batchSize);
            // TODO: доработать под ситуацию, когда в один слот попало более element per slot limit.
            var uniqueHandlerSet = new HashSet<string>(uniqueHandlersLimit ?? 0);

            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DbId);

            // Запускается не сразу, а после получения хотя бы одного сообщения.
            // Пока пустой - висит на асинхронном ожидании waitNewMessages.
            var stopwatch = new Stopwatch();

            while (true)
            {
                if (
                    buffer.Count >= batchSize
                    || (stopwatch.IsRunning && stopwatch.Elapsed > batchTimeout)
                    )
                {
                    break;
                }

                var queueWithMessages = state.GetQueueWithMessages();

                {
                    // 1) Проверяем наличие метаданные о непрочитанных сообщениях.
                    // Если QueueWithMessages пустой, то выставляем State.WaitNewMessage на ожидание.

                    if (!queueWithMessages.Any())
                    {
                        var waitNewMessages = await state.AllQueueEmptySleepAsync(cancellationToken);

                        if (!waitNewMessages.IsCompleted)
                        {
                            var timeout = stopwatch.IsRunning
                                ? batchTimeout - stopwatch.Elapsed
                                : (TimeSpan?)null;
                            var isNewMessage = await TimeoutHelper.WaitTaskAsync(
                                waitNewMessages,
                                timeout,
                                cancellationToken);

                            if (!isNewMessage)
                            {
                                // timeout
                                return buffer;
                            }

                            // Появились новые сообщения.
                        }
                    }
                }

                {
                    // 2) Пробуем считать из очередей.

                    var searchSets = queueWithMessages
                        .Take(_options.SearchSetsPerQueryLimit)
                        .Select(e => (
                            entry: e,
                            queueName: new RedisKey(_options.HandlerToQueueSetNameFactory(e.Key))
                            )
                            )
                        .ToArray();

                    if (!searchSets.Any())
                    {
                        continue;
                    }

                    // Пытаемся считать.
                    var freeSpace = batchSize - buffer.Count;

                    // TODO: timeout?
                    var consumedMessages = await db.SortedSetPopAsync(
                        searchSets.Select(e => e.queueName).ToArray(),
                        count: freeSpace,
                        order: Order.Ascending);

                    if (!consumedMessages.Entries.Any())
                    {
                        // Все указанные пустые, уже считаны другой нодой.
                        foreach (var elem in searchSets)
                        {
                            // Очередь пустая, считала другая нода.
                            state.QueueIsEmpty(elem.entry.Key, elem.entry.Value, cancellationToken);
                        }

                        continue;
                    }

                    var triggerTypeKey = _options.QueueSetNameToHandlerFactory(consumedMessages.Key);

                    if (!stopwatch.IsRunning)
                    {
                        // Таймер батча запускается только после первого полученного сообщения.
                        stopwatch.Start();
                    }

                    // Ограничение уникальных хендлеров.
                    if (uniqueHandlersLimit.HasValue && !uniqueHandlerSet.Contains(triggerTypeKey.HandlerName))
                    {
                        uniqueHandlerSet.Add(triggerTypeKey.HandlerName);
                        if (uniqueHandlerSet.Count > uniqueHandlersLimit.Value)
                        {
                            // При превышении уникальных хенддлеров - возвращаем заявки в очередь.
                            await InnerProduceAsync(
                                consumedMessages.Entries
                                    .Select(e => new ITriggerQueueProvider<TId>.MessageContainer(
                                        new ITriggerQueueProvider<TId>.MessageDto(
                                            _options.StringToId(e.Element),
                                            triggerTypeKey.HandlerName),
                                        isRangeTrigger: true // uniqueHandlersLimit только у Range.
                                        ))
                                    .ToArray(), 
                                checkLimit: false,
                                cancellationToken);

                            return buffer;
                        }
                    }

                    buffer.AddRange(
                        consumedMessages.Entries
                            .Select(e => new ITriggerQueueProvider<TId>.MessageDto(_options.StringToId(e.Element), HandlerKey: triggerTypeKey.HandlerName))
                            .ToArray());

                    foreach (var elem in searchSets)
                    {
                        if (elem.entry.Key != triggerTypeKey)
                        {
                            // Очередь пустая, считала другая нода.
                            state.QueueIsEmpty(elem.entry.Key, elem.entry.Value, cancellationToken);
                            continue;
                        }

                        // Их этой очереди мы считали элементы и она опустела.
                        if (consumedMessages.Entries.Length < freeSpace)
                        {
                            // Очередь из который читали опустела.
                            state.QueueIsEmpty(elem.entry.Key, elem.entry.Value, cancellationToken);
                        }

                        break;
                    }
                }
            }

            return buffer;
        }
    }
}
