using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.T2;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.T2
{
    public class RedisNotifyTriggerQueueState<TId>
        : IRedisNotifyTriggerQueueState
    {
        public IRedisNotifyTriggerQueueState.IHandler RangeTriggerState { get; }

        public IRedisNotifyTriggerQueueState.IHandler SignleTriggerState { get; }


        public RedisNotifyTriggerQueueState(
            IServiceProvider serviceProvider,
            ITriggerRegistry triggerRegistry,
            ITriggerHandlerFactory<TId> triggerHandlerFactory)
        {
            using (var scope = serviceProvider.CreateAsyncScope())
            {
                RangeTriggerState = new Handler(
                    triggerRegistry.GetAll()
                    .Where(e => triggerHandlerFactory.IsRangeHandler(scope.ServiceProvider, e.HandlerName))
                    .ToArray());

                SignleTriggerState = new Handler(
                    triggerRegistry.GetAll()
                    .Where(e => !triggerHandlerFactory.IsRangeHandler(scope.ServiceProvider, e.HandlerName))
                    .ToArray());
            }
        }

        private class Handler : IRedisNotifyTriggerQueueState.IHandler
        {
            /// <summary>
            /// Содержит данные об очередях, в которых должны быть сообщения.
            /// В порядке приоритета процессов.
            /// Key - процесс.
            /// Value - timestamp последнего события о поступлении сообщения.
            /// // TODO: PERF: ImmutableSortedDictionary vs ConcurrentSortedDictionary.
            /// </summary>
            private OptimisticLockContainer<ImmutableSortedDictionary<IRedisNotifyTriggerQueueState.KeyDto, long>> QueueWithMessages { get; }
            /// <summary>
            /// Содержит задачу для ожидания.
            /// Если все очереди опустели - Task переводится в состояние ожидания.
            /// Если поступает сообщение - Task переводится в состояние завершен.
            /// </summary>
            private LockContainer<TaskCompletionSource> WaitNewMessage { get; }

            public Handler(
                TriggerRegistryDto[] registries)
            {
                var waitNewMessageBuilder = ImmutableSortedDictionary.CreateBuilder<IRedisNotifyTriggerQueueState.KeyDto, long>(new KeyComparer());
                waitNewMessageBuilder.AddRange(
                    registries
                        .Select(e => new KeyValuePair<IRedisNotifyTriggerQueueState.KeyDto, long>(
                            new IRedisNotifyTriggerQueueState.KeyDto(e.HandlerName, 0),
                            DateTimeOffset.MinValue.UtcTicks))
                        .ToArray()
                    );
                var waitNewMessage = new TaskCompletionSource(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
                waitNewMessage.SetResult();

                QueueWithMessages = new OptimisticLockContainer<ImmutableSortedDictionary<IRedisNotifyTriggerQueueState.KeyDto, long>>(
                        waitNewMessageBuilder.ToImmutableSortedDictionary());
                WaitNewMessage = new LockContainer<TaskCompletionSource>(
                        waitNewMessage);
            }

            public ImmutableSortedDictionary<IRedisNotifyTriggerQueueState.KeyDto, long> GetQueueWithMessages()
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
                IRedisNotifyTriggerQueueState.KeyDto key,
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
                ICollection<IRedisNotifyTriggerQueueState.KeyDto> keys,
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
        }

        private class KeyComparer : IComparer<IRedisNotifyTriggerQueueState.KeyDto>
        {
            public int Compare(IRedisNotifyTriggerQueueState.KeyDto x, IRedisNotifyTriggerQueueState.KeyDto y)
            {
                var r1 = Comparer<short>.Default.Compare(x.Priority, y.Priority);
                if (r1 != 0)
                {
                    return r1;
                }

                return Comparer<string>.Default.Compare(x.HandlerName, y.HandlerName);
            }
        }
    }
}
