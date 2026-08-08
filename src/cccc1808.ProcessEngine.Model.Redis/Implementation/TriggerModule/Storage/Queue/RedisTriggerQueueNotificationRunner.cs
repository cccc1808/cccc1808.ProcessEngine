using System.Collections.Concurrent;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;
using cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.Queue;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.Storage.Queue
{
    public class RedisTriggerQueueNotificationRunner<TId> 
        : IRedisTriggerQueueNotificationRunner
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ITriggerRegistry _triggerRegistry;
        private readonly IRedisConnectionFactory _redisConnectionFactory;
        private readonly IRedisTriggerQueueNotifyState _state;
        private readonly ITriggerHandlerFactory<TId> _triggerHandlerFactory;

        private readonly RedisTriggerQueueOptionsDto<TId> _options;

        public RedisTriggerQueueNotificationRunner(
            IServiceProvider serviceProvider,
            ITriggerRegistry triggerRegistry,
            IRedisConnectionFactory redisConnectionFactory,
            IRedisTriggerQueueNotifyState state,
            ITriggerHandlerFactory<TId> triggerHandlerFactory,

            RedisTriggerQueueOptionsDto<TId> options)
        {
            _serviceProvider = serviceProvider;
            _triggerRegistry = triggerRegistry;
            _redisConnectionFactory = redisConnectionFactory;
            _state = state;
            _triggerHandlerFactory = triggerHandlerFactory;

            _options = options;
        }

        public async Task RunAsync(
            bool one,
            CancellationToken cancellationToken)
        {
            static string waitComplete(
                LinkContainer<(string, ConcurrentDictionary<string, Task>, Task)> state)
            {
                state.Data.Item2.TryAdd(state.Data.Item1, state.Data.Item3);

                return state.Data.Item1;
            }

            var allTriggers = _triggerRegistry.GetAll();

            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, cancellationToken);

            var subscribes = new Dictionary<string, NotificationEntryDto>(allTriggers.Count);
            var completeBuffer = new ConcurrentDictionary<string, Task>();
            var waitBuffer = new HashSet<Task>(allTriggers.Count);

            try
            {
                // 1) Подписываемся на оповещения по всем типам процессов.
                foreach (var elem in allTriggers)
                {
                    var subscribe = await connection.SubscribeAsync(_options.QueueChannelNameFactory(
                        new IRedisTriggerQueueNotifyState.KeyDto(elem.HandlerName, 0)),
                        cancellationToken);
                    var enumerator = subscribe.ChannelMessages.GetAsyncEnumerator(cancellationToken);

                    var entry = new NotificationEntryDto()
                    {
                        TriggerRegistryDto = elem,
                        IsRangeTrigger = _triggerHandlerFactory.IsRangeHandler(_serviceProvider, elem.HandlerName),
                        Subsribe = subscribe,
                        Enumerator = enumerator,
                    };

                    var waitTaskContainer = LinkContainer.Create<(string, ConcurrentDictionary<string, Task>, Task)>(default);
                    var waitTask = enumerator.MoveNextAsync().AsTask()
                        .ContinueWith(
                            static (t, s) => waitComplete((LinkContainer<(string, ConcurrentDictionary<string, Task>, Task)>)s!),
                            state: waitTaskContainer,
                            continuationOptions: TaskContinuationOptions.RunContinuationsAsynchronously);
                    waitTaskContainer.Data = (elem.HandlerName, completeBuffer, waitTask);

                    subscribes.Add(elem.HandlerName, entry);
                    waitBuffer.Add(waitTask);
                }

                // 2) Считываем обновления по очередям.
                var rangeNotifyBuffer = new List<IRedisTriggerQueueNotifyState.KeyDto>();
                var singleNotifyBuffer = new List<IRedisTriggerQueueNotifyState.KeyDto>();
                while (true)
                {
                    rangeNotifyBuffer.Clear();
                    singleNotifyBuffer.Clear();

                    // Ждем оповещения о поступлении сообщения в очередь.
                    await Task.WhenAny(waitBuffer);

                    foreach (var elem in completeBuffer)
                    {
                        var key = elem.Key;
                        completeBuffer.TryRemove(elem.Key, out _);                        

                        var subscribe = subscribes[key];
                        if (subscribe.IsRangeTrigger)
                        {
                            rangeNotifyBuffer.Add(new IRedisTriggerQueueNotifyState.KeyDto(key, 0));
                        }
                        else 
                        {
                            singleNotifyBuffer.Add(new IRedisTriggerQueueNotifyState.KeyDto(key, 0));
                        }
                            
                        var waitTaskContainer = LinkContainer.Create<(string, ConcurrentDictionary<string, Task>, Task)>(default);
                        var newWaitTask = subscribe.Enumerator.MoveNextAsync().AsTask()
                            .ContinueWith(
                                static (t, s) => waitComplete((LinkContainer<(string, ConcurrentDictionary<string, Task>, Task)>)s!),
                                state: waitTaskContainer,
                                continuationOptions: TaskContinuationOptions.RunContinuationsAsynchronously);
                        waitTaskContainer.Data = (key, completeBuffer, newWaitTask);

                        waitBuffer.Remove(elem.Value);
                        waitBuffer.Add(newWaitTask);
                    }

                    // Обновляем метаданные о поступлении нового сообщения.
                    if (rangeNotifyBuffer.Any())
                    {
                        await _state.RangeTriggerState.NewMessageInQueueAsync(rangeNotifyBuffer, cancellationToken);
                    }
                    if (singleNotifyBuffer.Any())
                    {
                        await _state.SignleTriggerState.NewMessageInQueueAsync(singleNotifyBuffer, cancellationToken);
                    }

                    if (one)
                    {
                        return;
                    }
                }                
            }
            finally
            {
                foreach (var elem in subscribes.Values)
                {
                    // await elem.Enumerator.DisposeAsync();
                    await elem.Subsribe.DisposeAsync();
                }
            }
        }

        private class NotificationEntryDto
        {
            public required TriggerRegistryDto TriggerRegistryDto { get; init; }

            public required bool IsRangeTrigger { get; set; }

            public required IRedisConnection.ISubscribeContainer Subsribe { get; init; }

            public required IAsyncEnumerator<ChannelMessage> Enumerator { get; init; }
        }
    }
}
