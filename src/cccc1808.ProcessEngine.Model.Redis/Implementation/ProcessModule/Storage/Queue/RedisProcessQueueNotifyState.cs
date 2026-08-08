using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule.Queue;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.ProcessModule.Storage.Queue
{
    public class RedisProcessQueueNotifyState
        : IRedisProcessQueueNotifyState
    {
        public IRedisProcessQueueNotifyState.IHandler RangeHandler { get; }

        public IRedisProcessQueueNotifyState.IHandler SingleHandler { get; }

        public RedisProcessQueueNotifyState(IProcessRegistry processRegistry)
        {
            var registries = processRegistry.All();

            RangeHandler = new Handler(
                registries.Where(e => !e.Metadata.IsSignleExecuteProcess).ToArray());
            SingleHandler = new Handler(
                registries.Where(e => e.Metadata.IsSignleExecuteProcess).ToArray());
        }

        private class Handler : IRedisProcessQueueNotifyState.IHandler
        {
            /// <summary>
            /// Содержит данные об очередях, в которых должны быть сообщения.
            /// В порядке приоритета процессов.
            /// Key - процесс.
            /// Value - timestamp последнего события о поступлении сообщения.
            /// // TODO: PERF: ImmutableSortedDictionary vs ConcurrentSortedDictionary.
            /// </summary>
            private OptimisticLockContainer<ImmutableSortedDictionary<ProcessTypeUniqueDto, long>> QueueWithMessages { get; }
            /// <summary>
            /// Содержит задачу для ожидания.
            /// Если все очереди опустели - Task переводится в состояние ожидания.
            /// Если поступает сообщение - Task переводится в состояние завершен.
            /// </summary>
            private LockContainer<TaskCompletionSource> WaitNewMessage { get; }

            public Handler(
                ProcessRegistryDto[] processRegistries)
            {
                var waitNewMessageBuilder = ImmutableSortedDictionary.CreateBuilder<ProcessTypeUniqueDto, long>(
                    new PriorityComparer());
                waitNewMessageBuilder.AddRange(
                    processRegistries
                        .Select(e => new KeyValuePair<ProcessTypeUniqueDto, long>(
                            e.Unique, 
                            DateTimeOffset.MinValue.UtcTicks
                            )
                        )
                        .ToArray()
                    );
                var waitNewMessage = new TaskCompletionSource(
                    creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
                waitNewMessage.SetResult();

                QueueWithMessages = new OptimisticLockContainer<ImmutableSortedDictionary<ProcessTypeUniqueDto, long>>(
                    waitNewMessageBuilder.ToImmutableSortedDictionary());
                WaitNewMessage = new LockContainer<TaskCompletionSource>(
                    waitNewMessage);
            }

            public ImmutableSortedDictionary<ProcessTypeUniqueDto, long> GetQueueWithMessages()
            {
                return QueueWithMessages.Data;
            }

            public async Task<Task> AllQueueEmptySleepAsync(CancellationToken cancellationToken)
            {
                var waitNewMessages = await WaitNewMessage.DoubleCheckPatternAsync(
                    this,
                    static (p, e) => p.QueueWithMessages.Data.Any() && e.Task.IsCompleted,
                    static (p, _) => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
                    cancellationToken);
                return waitNewMessages.Task;
            }

            public void QueueIsEmpty(
                ProcessTypeUniqueDto key,
                long timestamp,
                CancellationToken cancellationToken)
            {
                QueueWithMessages.TryUpdate(
                    (Key: key, Value: timestamp),
                    static (p, e) =>
                    {
                        // Пробуем пометить очередт как пустую.
                        if (e.TryGetValue(p.Key, out var storeValue) && storeValue == p.Value)
                        {
                            return e.Remove(p.Key);
                        }

                        return e;
                    },
                    cancellationToken);
            }

            public async ValueTask NewMessageInQueueAsync(
                ICollection<ProcessTypeUniqueDto> keys,
                CancellationToken cancellationToken)
            {
                foreach (var elem in keys)
                {
                    // Обновляем набор непустых очередей.
                    QueueWithMessages.TryUpdate(
                        elem,
                        // TODO: datetime
                        static (p, e) => e.SetItem(p, DateTimeOffset.UtcNow.UtcTicks),
                        cancellationToken);
                }

                // Помечаем wait task как завершенный.
                await WaitNewMessage.DoubleCheckPatternAsync(
                    1,
                    static (p, e) => e.Task.IsCompleted,
                    static (p, e) =>
                    {
                        e.SetResult();
                        return e;
                    },
                    cancellationToken);
            }

            public void Clear()
            {
                QueueWithMessages.TryUpdate(1, (p, e) => e.Clear(), CancellationToken.None);
            }
        }

        private class PriorityComparer : IComparer<ProcessTypeUniqueDto>
        {
            public int Compare(ProcessTypeUniqueDto x, ProcessTypeUniqueDto y)
            {
                var r1 = Comparer<int>.Default.Compare(x.Priority, y.Priority);
                if (r1 != 0)
                {
                    return r1;
                }

                var r2 = Comparer<long>.Default.Compare(x.ProcessType.ProcessType, y.ProcessType.ProcessType);
                if (r2 != 0) 
                {
                    return r2;
                }

                return Comparer<int>.Default.Compare(x.ProcessType.ProcessVersion, y.ProcessType.ProcessVersion);
            }
        }
    }
}
