using System.Collections.Concurrent;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.ProcessModule.T2
{
    public class RedisQueueNotificationRunner<TId> : IRedisQueueNotificationRunner
    {
        private readonly IProcessRegistry _processRegistry;
        private readonly IRedisConnectionFactory _redisConnectionFactory;
        private readonly IRedisNotifyQueueState _state;

        private readonly QOptionsDto<TId> _options;

        public RedisQueueNotificationRunner(
            IProcessRegistry processRegistry,
            IRedisConnectionFactory redisConnectionFactory,
            IRedisNotifyQueueState state,

            QOptionsDto<TId> options)
        {
            _processRegistry = processRegistry;
            _redisConnectionFactory = redisConnectionFactory;
            _state = state;

            _options = options;
        }

        public async Task RunAsync(
            bool one,
            CancellationToken cancellationToken)
        {
            static ProcessRegistryDto waitComplete(
                LinkContainer<(ProcessRegistryDto, ConcurrentDictionary<ProcessRegistryDto, Task>, Task)> state)
            {
                state.Data.Item2.TryAdd(state.Data.Item1, state.Data.Item3);

                return state.Data.Item1;
            }

            var allProcesses = _processRegistry.All();

            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, cancellationToken);

            var subscribers = new Dictionary<ProcessRegistryDto, NotificationEntryDto>(allProcesses.Count);
            var completeBuffer = new ConcurrentDictionary<ProcessRegistryDto, Task>();
            var waitBuffer = new HashSet<Task>(allProcesses.Count);

            try
            {
                // 1) Подписываемся на оповещения по всем типам процессов.
                foreach (var elem in allProcesses)
                {
                    var subsribe = await connection.SubscribeAsync(_options.QueueChannelNameFactory(elem), cancellationToken);
                    var enumerator = subsribe.ChannelMessages.GetAsyncEnumerator(cancellationToken);

                    var entry = new NotificationEntryDto()
                    {
                        ProcessRegistry = elem,
                        Subsribe = subsribe,
                        Enumerator = enumerator,
                    };

                    var waitTaskContainer = LinkContainer.Create<(ProcessRegistryDto, ConcurrentDictionary<ProcessRegistryDto, Task>, Task)>(default);
                    var waitTask = enumerator.MoveNextAsync().AsTask()
                        .ContinueWith(
                            static (t, s) => waitComplete((LinkContainer<(ProcessRegistryDto, ConcurrentDictionary<ProcessRegistryDto, Task>, Task)>)s!),
                            state: waitTaskContainer,
                            continuationOptions: TaskContinuationOptions.ExecuteSynchronously);
                    waitTaskContainer.Data = (elem, completeBuffer, waitTask);

                    subscribers.Add(elem, entry);
                    waitBuffer.Add(waitTask);
                }

                // 2) Считываем обновления по очередям.
                var notifyBuffer = new List<ProcessRegistryDto>();
                while (true)
                {
                    notifyBuffer.Clear();

                    // Ждем оповещения о поступлении сообщения в очередь.
                    await Task.WhenAny(waitBuffer);

                    foreach (var elem in completeBuffer)
                    {
                        var key = elem.Key;
                        completeBuffer.TryRemove(elem.Key, out _);
                        notifyBuffer.Add(key);

                        var subscribe = subscribers[key];

                        var waitTaskContainer = LinkContainer.Create<(ProcessRegistryDto, ConcurrentDictionary<ProcessRegistryDto, Task>, Task)>(default);
                        var newWaitTask = subscribe.Enumerator.MoveNextAsync().AsTask()
                            .ContinueWith(
                                static (t, s) => waitComplete((LinkContainer<(ProcessRegistryDto, ConcurrentDictionary<ProcessRegistryDto, Task>, Task)>)s!),
                                state: waitTaskContainer,
                                continuationOptions: TaskContinuationOptions.ExecuteSynchronously);
                        waitTaskContainer.Data = (key, completeBuffer, newWaitTask);

                        waitBuffer.Remove(elem.Value);
                        waitBuffer.Add(newWaitTask);
                    }

                    // Обновляем метаданные о поступлении нового сообщения.
                    await _state.NewMessageInQueueAsync(notifyBuffer, cancellationToken);

                    if (one)
                    {
                        return;
                    }
                }                
            }
            finally
            {
                foreach (var elem in subscribers.Values)
                {
                    // await elem.Enumerator.DisposeAsync();
                    await elem.Subsribe.DisposeAsync();
                }
            }
        }

        private class NotificationEntryDto
        {
            public required ProcessRegistryDto ProcessRegistry { get; init; }

            public required IRedisConnection.ISubscribeContainer Subsribe { get; init; }

            public required IAsyncEnumerator<ChannelMessage> Enumerator { get; init; }
        }
    }
}
