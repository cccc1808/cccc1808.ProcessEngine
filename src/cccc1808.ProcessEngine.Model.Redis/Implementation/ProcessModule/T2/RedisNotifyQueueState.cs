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

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.ProcessModule.T2
{
    public class RedisNotifyQueueState
        : IRedisNotifyQueueState
    {
        /// <summary>
        /// Содержит данные об очередях, в которых должны быть сообщения.
        /// В порядке приоритета процессов.
        /// Key - процесс.
        /// Value - timestamp последнего события о поступлении сообщения.
        /// </summary>
        private OptimisticLockContainer<ImmutableSortedDictionary<ProcessRegistryDto, long>> QueueWithMessages { get; }
        /// <summary>
        /// Содержит задачу для ожидания.
        /// Если все очереди опустели - Task переводится в состояние ожидания.
        /// Если поступает сообщение - Task переводится в состояние завершен.
        /// </summary>
        private LockContainer<TaskCompletionSource> WaitNewMessage { get; }

        public RedisNotifyQueueState(
            IProcessRegistry processRegistry)
        {
            var waitNewMessageBuilder = ImmutableSortedDictionary.CreateBuilder<ProcessRegistryDto, long>(new PriorityComparer());
            waitNewMessageBuilder.AddRange(
                processRegistry.All()
                    .Select(e => new KeyValuePair<ProcessRegistryDto, long>(e, DateTimeOffset.MinValue.UtcTicks))
                    .ToArray()
                );
            var waitNewMessage = new TaskCompletionSource(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
            waitNewMessage.SetResult();

            QueueWithMessages = new OptimisticLockContainer<ImmutableSortedDictionary<ProcessRegistryDto, long>>(
                    waitNewMessageBuilder.ToImmutableSortedDictionary());
            WaitNewMessage = new LockContainer<TaskCompletionSource>(
                    waitNewMessage);
        }

        public ImmutableSortedDictionary<ProcessRegistryDto, long> GetQueueWithMessages()
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
            ProcessRegistryDto key,
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
            ICollection<ProcessRegistryDto> keys,
            CancellationToken cancellationToken)
        {
            foreach (var elem in keys)
            {
                // Обновляем набор непустых очередей.
                QueueWithMessages.TryUpdate(
                    elem,
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

        private class PriorityComparer : IComparer<ProcessRegistryDto>
        {
            public int Compare(ProcessRegistryDto? x, ProcessRegistryDto? y)
            {
                return Comparer<int>.Default.Compare(x.Priority, y.Priority);
            }
        }
    }
}
