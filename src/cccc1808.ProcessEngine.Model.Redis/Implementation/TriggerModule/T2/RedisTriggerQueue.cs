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
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;
using cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.T2;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.T2
{
    public class RedisTriggerQueue<TId>
        : ITriggerQueue<TId>
    {
        private readonly IRedisConnectionFactory _redisConnectionFactory;
        private readonly IRedisNotifyTriggerQueueState _state;

        private readonly TriggerQueueOptionsDto<TId> _options;

        public RedisTriggerQueue(
            IRedisConnectionFactory redisConnectionFactory,
            IRedisNotifyTriggerQueueState state,

            TriggerQueueOptionsDto<TId> options)
        {
            _redisConnectionFactory = redisConnectionFactory;
            _state = state;

            _options = options;
        }

        public async Task<List<ITriggerQueue<TId>.MessageDto>> ConsumeRangeTriggersAsync(
            int batchLimit,
            int uniqueHandlersLimit,
            TimeSpan timeout,            
            CancellationToken cancellationToken)
        {
            return await InnerConsumeAsync(_state.RangeTriggerState, batchLimit, uniqueHandlersLimit, timeout, cancellationToken);
        }

        public async Task<List<ITriggerQueue<TId>.MessageDto>> ConsumeSignleTriggersAsync(
            int batchLimit,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return await InnerConsumeAsync(_state.SignleTriggerState, batchLimit, null, timeout, cancellationToken);
        }

        public async Task<bool> ProduceActivatedTriggerAsync(
            ICollection<ITriggerQueue<TId>.MessageContainer> messages,
            CancellationToken cancellationToken)
        {
            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DbId);

            var isFull = false;

            var groups = messages.GroupBy(e => new IRedisNotifyTriggerQueueState.KeyDto(e.Message.HandlerKey, 0))
                .ToDictionary(e => e.Key, e => e);

            // 1) Проверка свободного места.
            var pipline = new Dictionary<IRedisNotifyTriggerQueueState.KeyDto, Task<long>>(groups.Count);
            foreach (var elem in groups)
            {
                var t = db.SortedSetLengthAsync(_options.HandlerToQueueSetNameFactory(elem.Key));
                pipline.Add(elem.Key, t);
            }
            await connection.WaitPiplineWithTimeoutAsync(pipline.Values, cancellationToken);
            foreach (var elem in pipline)
            {
                if (elem.Value.Result >= _options.QueueSizeLimit)
                {
                    // Очередь заполнена.
                    isFull = true;
                    groups.Remove(elem.Key);
                }
            }

            // 2) Публикуем сообщения.
            pipline.Clear();
            foreach (var elem in groups)
            {
                var t = db.SortedSetUpdateAsync(
                    _options.HandlerToQueueSetNameFactory(elem.Key),
                    // TODO: score можно указывать на основе LastProcessedDate timestamp (чтобы элементы размещались в пордяке даты последней обработки).
                    elem.Value
                        .Select(e => new SortedSetEntry(_options.IdToString(e.Message.TriggerId), score: -1))
                        .ToArray(),
                    when: SortedSetWhen.NotExists);

                pipline.Add(elem.Key, t);
            }
            await connection.WaitPiplineWithTimeoutAsync(pipline.Values, cancellationToken);

            // 3) Публикуем оповещения.
            // TODO: наверное можно совместить с pipline 2 (меньше запросов).
            var notify = groups
                .Select(
                    e => new KeyValuePair<string, JsonElement[]>(
                        _options.QueueChannelNameFactory(e.Key),
                        [JsonHelper.EmptyObject]
                        )
                    )
                .ToArray();
            await connection.PubAsync(notify, cancellationToken);

            return isFull;
        }

        private async Task<List<ITriggerQueue<TId>.MessageDto>> InnerConsumeAsync(
            IRedisNotifyTriggerQueueState.IHandler state,
            int batchSize,
            int? uniqueHandlersLimit,
            TimeSpan batchTimeout,
            CancellationToken cancellationToken)
        {
            var buffer = new List<ITriggerQueue<TId>.MessageDto>(batchSize);
            var uniqueHandlerSet = new HashSet<IRedisNotifyTriggerQueueState.KeyDto>(uniqueHandlersLimit ?? 0);

            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DbId);


            var stopwatch = new Stopwatch();

            while (buffer.Count < batchSize && stopwatch.Elapsed < batchTimeout)
            {
                var queueWithMessages = state.GetQueueWithMessages();

                {
                    // 1) Проверяем наличие метаданные о непрочитанных сообщениях.
                    // Если QueueWithMessages пустой, то выставляем State.WaitNewMessage на ожидание.

                    if (!queueWithMessages.Any())
                    {
                        var waitNewMessages = state.AllQueueEmptySleepAsync(cancellationToken);

                        if (!waitNewMessages.IsCompleted)
                        {
                            var timeoutTask = Task.Delay(batchTimeout - stopwatch.Elapsed, cancellationToken);
                            var completedTask = await Task.WhenAny(
                                waitNewMessages,
                                timeoutTask);

                            if (completedTask == timeoutTask)
                            {
                                // timeout
                                return buffer;
                            }
                            else
                            {
                                // Появились новые сообщения.
                            }
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

                    buffer.AddRange(
                        consumedMessages.Entries
                            .Select(e => new ITriggerQueue<TId>.MessageDto(_options.StringToId(e.Element), HandlerKey: triggerTypeKey.HandlerName))
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

                            if (uniqueHandlersLimit.HasValue && !uniqueHandlerSet.Contains(triggerTypeKey))
                            {
                                uniqueHandlerSet.Add(triggerTypeKey);
                                if (uniqueHandlerSet.Count >= uniqueHandlersLimit.Value)
                                {
                                    return buffer;
                                }
                            }
                        }

                        break;
                    }

                    if (uniqueHandlersLimit.HasValue && !uniqueHandlerSet.Contains(triggerTypeKey))
                    {
                        uniqueHandlerSet.Add(triggerTypeKey);
                        if (uniqueHandlerSet.Count > uniqueHandlersLimit.Value)
                        {
                            return buffer;
                        }
                    }
                }
            }

            return buffer;
        }
    }
}
