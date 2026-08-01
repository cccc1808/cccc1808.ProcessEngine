using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.ProcessModule.T2
{
    public class RedisReservationQueue<TId> 
        : IRedisReservationQueue<TId>
    {
        private readonly IRedisConnectionFactory _redisConnectionFactory;
        private readonly IRedisNotifyQueueState _state;

        private readonly QOptionsDto<TId> _options;        

        public RedisReservationQueue(
            IRedisConnectionFactory redisConnectionFactory,
            IRedisNotifyQueueState state,

            QOptionsDto<TId> options)
        {
            _redisConnectionFactory = redisConnectionFactory;
            _state = state;

            _options = options;
        }        

        public async Task<List<IRedisReservationQueue<TId>.MessageDto>> ConsumeAsync(
            int batchSize,
            TimeSpan batchTimeout,
            CancellationToken cancellationToken)
        {
            var buffer = new List<IRedisReservationQueue<TId>.MessageDto>(batchSize);

            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DbId);

            {
                var stopwatch = Stopwatch.StartNew();

                while (buffer.Count < batchSize && stopwatch.Elapsed < batchTimeout)
                {
                    var queueWithMessages = _state.GetQueueWithMessages();

                    {
                        // 1) Проверяем наличие метаданные о непрочитанных сообщениях.
                        // Если QueueWithMessages пустой, то выставляем State.WaitNewMessage на ожидание.

                        if (!queueWithMessages.Any())
                        {
                            var waitNewMessages = _state.AllQueueEmptySleepAsync(cancellationToken);

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
                                queueName: new RedisKey(_options.ProcessToQueueSetNameFactory(e.Key))
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
                                _state.QueueIsEmpty(elem.entry.Key, elem.entry.Value, cancellationToken);
                            }

                            continue;
                        }

                        var processKey = _options.QueueSetNameToProcessTypeFactory(consumedMessages.Key);
                        buffer.AddRange(
                            consumedMessages.Entries
                                .Select(e => new IRedisReservationQueue<TId>.MessageDto(processKey, _options.StringToId(e.Element)))
                                .ToArray());

                        foreach (var elem in searchSets)
                        {
                            if (elem.entry.Key != processKey)
                            {
                                // Очередь пустая, считала другая нода.
                                _state.QueueIsEmpty(elem.entry.Key, elem.entry.Value, cancellationToken);
                            }
                            else
                            {
                                // Их этой очереди мы считали элементы.
                                if (consumedMessages.Entries.Length < freeSpace)
                                {
                                    // Очередь из который читали опустела.
                                    _state.QueueIsEmpty(elem.entry.Key, elem.entry.Value, cancellationToken);
                                }

                                break;
                            }
                        }
                    }
                }

                stopwatch.Stop();
            }

            return buffer;
        }

        public async Task<ICollection<IRedisReservationQueue<TId>.MessageDto>> ProduceAsync(
            ICollection<IRedisReservationQueue<TId>.MessageDto> processes,
            CancellationToken cancellationToken)
        {
            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DbId);

            var notSended = new List<IRedisReservationQueue<TId>.MessageDto>(0);
            var groups = processes.GroupBy(e => e.Registry)
                .ToDictionary(e => e.Key, e => e);

            // 1) Проверка свободного места.
            var pipline = new Dictionary<ProcessRegistryDto, Task<long>>(groups.Count);
            foreach (var elem in groups)
            {
                var t = db.SortedSetLengthAsync(_options.ProcessToQueueSetNameFactory(elem.Key));
                pipline.Add(elem.Key, t);
            }
            await connection.WaitPiplineWithTimeoutAsync(pipline.Values, cancellationToken);
            foreach (var elem in pipline)
            {
                if (elem.Value.Result >= _options.QueueSizeLimit)
                {
                    // Очередь заполнена.
                    var group = groups[elem.Key];
                    notSended.AddRange(group);
                    groups.Remove(elem.Key);
                }
            }

            // 2) Публикуем сообщения.
            pipline.Clear();
            foreach (var elem in groups)
            {
                var t = db.SortedSetUpdateAsync(
                    _options.ProcessToQueueSetNameFactory(elem.Key),
                    // TODO: score можно указывать на основе LastProcessedDate timestamp (чтобы элементы размещались в пордяке даты последней обработки).
                    elem.Value
                        .Select(e => new SortedSetEntry(_options.IdToString(e.ProcessId), score: -1))
                        .ToArray(),
                    when: SortedSetWhen.NotExists);

                pipline.Add(elem.Key, t);
            }
            await connection.WaitPiplineWithTimeoutAsync(pipline.Values, cancellationToken);

            // 3) Публикуем оповещения.
            // TODO: наверное можно совместить с pipline 2 (меньше запросов).
            var messages = groups
                .Select(
                    e => new KeyValuePair<string, JsonElement[]>(
                        _options.QueueChannelNameFactory(e.Key),
                        [JsonHelper.EmptyObject]
                        )
                    )
                .ToArray();
            await connection.PubAsync(messages, cancellationToken);

            return notSended;
        }                
    }
}
