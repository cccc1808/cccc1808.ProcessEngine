using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.Queue;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.Storage.Queue
{
    public class RedisTriggerQueueNotifyState<TId>
        : IRedisTriggerQueueNotifyState
    {
        public IRedisTriggerQueueNotifyState.IHandler RangeTriggerState { get; }

        public IRedisTriggerQueueNotifyState.IHandler SignleTriggerState { get; }

        public RedisTriggerQueueNotifyState(
            IServiceProvider serviceProvider,
            IDateTimeProvider dateTimeProvider,
            ITriggerRegistry triggerRegistry,
            ITriggerHandlerFactory<TId> triggerHandlerFactory)
        {
            using (var scope = serviceProvider.CreateAsyncScope())
            {
                RangeTriggerState = new Handler(
                    dateTimeProvider,
                    triggerRegistry.GetAll()
                        .Where(e => triggerHandlerFactory.IsRangeHandler(scope.ServiceProvider, e.Unique.HandlerName))
                        .ToArray()
                        );

                SignleTriggerState = new Handler(
                    dateTimeProvider,
                    triggerRegistry.GetAll()
                        .Where(e => !triggerHandlerFactory.IsRangeHandler(scope.ServiceProvider, e.Unique.HandlerName))
                        .ToArray()
                        );
            }
        }

        private class Handler : IRedisTriggerQueueNotifyState.IHandler
        {
            private readonly IDateTimeProvider _dateTimeProvider;

            /// <summary>
            /// Содержит данные об очередях, в которых должны быть сообщения.
            /// В порядке приоритета процессов.
            /// Key - процесс.
            /// Value - timestamp последнего события о поступлении сообщения.
            /// // TODO: PERF: ImmutableSortedDictionary vs ConcurrentSortedDictionary.
            /// </summary>
            private OptimisticLockContainer<ImmutableSortedDictionary<TriggerTypeUniqueDto, long>> QueueWithMessages { get; }
            /// <summary>
            /// Содержит задачу для ожидания.
            /// Если все очереди опустели - Task переводится в состояние ожидания.
            /// Если поступает сообщение - Task переводится в состояние завершен.
            /// </summary>
            private LockContainer<TaskCompletionSource> WaitNewMessage { get; }

            public Handler(
                IDateTimeProvider dateTimeProvider,
                TriggerRegistryDto[] registries)
            {
                _dateTimeProvider = dateTimeProvider;

                var waitNewMessageBuilder = ImmutableSortedDictionary.CreateBuilder<TriggerTypeUniqueDto, long>(
                    new KeyComparer());
                waitNewMessageBuilder.AddRange(
                    registries
                        .Select(e => new KeyValuePair<TriggerTypeUniqueDto, long>(
                            e.Unique,
                            DateTimeOffset.MinValue.UtcTicks))
                        .ToArray()
                    );
                var waitNewMessage = new TaskCompletionSource(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
                waitNewMessage.SetResult();

                QueueWithMessages = new OptimisticLockContainer<ImmutableSortedDictionary<TriggerTypeUniqueDto, long>>(
                        waitNewMessageBuilder.ToImmutableSortedDictionary());
                WaitNewMessage = new LockContainer<TaskCompletionSource>(
                        waitNewMessage);
            }

            public ImmutableSortedDictionary<TriggerTypeUniqueDto, long> GetQueueWithMessages()
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
                TriggerTypeUniqueDto key,
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
                ICollection<TriggerTypeUniqueDto> keys,
                CancellationToken cancellationToken)
            {
                foreach (var elem in keys)
                {
                    // Обновляем набор непустых очередей.
                    QueueWithMessages.TryUpdate(
                        (This: this, elem),
                        static (p, e) => e.SetItem(p.elem, p.This._dateTimeProvider.UtcNow.UtcTicks),
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
                QueueWithMessages.TryUpdate(1, (p, e) => e.Clear(), default);
            }
        }

        private class KeyComparer : IComparer<TriggerTypeUniqueDto>
        {
            public int Compare(TriggerTypeUniqueDto x, TriggerTypeUniqueDto y)
            {
                var r1 = Comparer<short>.Default.Compare(x.Priority, y.Priority);
                if (r1 != 0)
                {
                    return r1;
                }

                var r2 = Comparer<string>.Default.Compare(x.HandlerName, y.HandlerName);
                return r2;
            }
        }
    }
}
