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

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Extensions;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;
using cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule.Queue;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.ProcessModule.Storage.Queue
{
    public class RedisProcessQueueProvider<TId> 
        : IProcessQueueProvider<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IRedisConnectionFactory _redisConnectionFactory;
        private readonly IRedisNotifyProcessQueueState _state;

        private readonly ProcessQueueOptionsDto<TId> _options;        

        public RedisProcessQueueProvider(
            IServiceProvider serviceProvider,
            IRedisConnectionFactory redisConnectionFactory,
            IRedisNotifyProcessQueueState state,

            ProcessQueueOptionsDto<TId> options)
        {
            _serviceProvider = serviceProvider;
            _redisConnectionFactory = redisConnectionFactory;
            _state = state;

            _options = options;
        }        

        public async Task<List<IProcessQueueProvider<TId>.MessageDto>> ConsumeRangeAsync(
            int batchSize, 
            int uniqueLimit, 
            TimeSpan batchTimeout, 
            CancellationToken cancellationToken)
        {
            return await InnerConsumeAsync(_state.RangeHandler, batchSize, uniqueLimit, batchTimeout, cancellationToken);
        }

        public async Task<List<IProcessQueueProvider<TId>.MessageDto>> ConsumeSignleAsync(
            int batchSize, 
            TimeSpan batchTimeout,
            CancellationToken cancellationToken)
        {
            return await InnerConsumeAsync(_state.SingleHandler, batchSize, null, batchTimeout, cancellationToken);
        }

        public async Task<HashSet<TId>> ProduceAsync(
            ICollection<IProcessQueueProvider<TId>.MessageDto> processes,
            CancellationToken cancellationToken)
        {
            return await InnerProduceAsync(processes, checkLimit: true, cancellationToken);
        }

        private async Task<HashSet<TId>> InnerProduceAsync(
            ICollection<IProcessQueueProvider<TId>.MessageDto> messages,
            bool checkLimit,
            CancellationToken cancellationToken)
        {
            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DbId);

            var notSended = new HashSet<TId>(0);

            var groups = messages.GroupBy(e => e.Unique)
                .ToDictionary(e => e.Key, e => e.ToArray());

            // 1) Проверка свободного места (не строгая).
            var pipline = new List<Task>(groups.Count * 2);
            {
                if (checkLimit)
                {
                    var lenghtTasks = new Dictionary<ProcessTypeUniqueDto, Task<long>>(groups.Count);
                    foreach (var elem in groups)
                    {
                        var t = db.SortedSetLengthAsync(_options.ProcessToQueueSetNameFactory(elem.Key));

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

                            notSended.AddRange(
                                notSendGroup, 
                                static (m) => m.ProcessId);

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
                        _options.ProcessToQueueSetNameFactory(elem.Key),
                        // TODO: score можно указывать на основе LastProcessedDate timestamp (чтобы элементы размещались в пордяке даты последней обработки).
                        elem.Value
                            .Select(e => new SortedSetEntry(_options.IdToString(e.ProcessId), score: -1))
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

        private async Task<List<IProcessQueueProvider<TId>.MessageDto>> InnerConsumeAsync(
            IRedisNotifyProcessQueueState.IHandler state,
            int batchSize,
            int? uniqueHandlersLimit,
            TimeSpan batchTimeout,
            CancellationToken cancellationToken)
        {
            var buffer = new List<IProcessQueueProvider<TId>.MessageDto>(batchSize);
            var uniqueHandlerSet = new HashSet<ProcessTypeUniqueDto>(uniqueHandlersLimit ?? 0);

            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DbId);

            // Запускается не сразу, а после получения хотя бы одного сообщения.
            // Пока пустой - висит на асинхронном ожидании waitNewMessages.
            var stopwatch = new Stopwatch();

            while (true)
            {
                if (
                    buffer.Count >= batchSize
                    || stopwatch.IsRunning && stopwatch.Elapsed > batchTimeout
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
                            state.QueueIsEmpty(elem.entry.Key, elem.entry.Value, cancellationToken);
                        }

                        continue;
                    }

                    var processTypeKey = _options.QueueSetNameToProcessTypeFactory(consumedMessages.Key);

                    if (!stopwatch.IsRunning)
                    {
                        // Таймер батча запускается только после первого полученного сообщения.
                        stopwatch.Start();
                    }

                    var typedMessages = consumedMessages.Entries
                        .Select(e => new IProcessQueueProvider<TId>.MessageDto(processTypeKey, _options.StringToId(e.Element)))
                        .ToArray();

                    // Ограничение уникальных хендлеров.
                    if (uniqueHandlersLimit.HasValue && !uniqueHandlerSet.Contains(processTypeKey))
                    {
                        uniqueHandlerSet.Add(processTypeKey);
                        if (uniqueHandlerSet.Count > uniqueHandlersLimit.Value)
                        {
                            // При превышении уникальных хенддлеров - возвращаем заявки в очередь.
                            await InnerProduceAsync(
                                typedMessages,
                                checkLimit: false,
                                cancellationToken);

                            return buffer;
                        }
                    }

                    buffer.AddRange(typedMessages);

                    foreach (var elem in searchSets)
                    {
                        if (elem.entry.Key != processTypeKey)
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
