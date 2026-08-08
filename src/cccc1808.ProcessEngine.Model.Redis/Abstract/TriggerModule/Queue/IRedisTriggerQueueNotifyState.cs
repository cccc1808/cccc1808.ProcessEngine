using System.Collections.Immutable;

namespace cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.Queue
{
    /// <summary>
    /// Состояние для работы очереди триггеров.
    /// Хранит:
    /// 1) Есть ли хотя бы одна непустая очередь (или Task асинхронного ожидания).
    /// 2) Какие очереди предположительно не пустые.
    /// </summary>
    public interface IRedisTriggerQueueNotifyState
    {
        IHandler RangeTriggerState { get; }

        IHandler SignleTriggerState { get; }

        public interface IHandler
        {
            /// <summary>
            /// Получить данные об предположительно не пустых очередях.
            /// </summary>
            /// <returns></returns>
            ImmutableSortedDictionary<KeyDto, long> GetQueueWithMessages();

            /// <summary>
            /// Зафиксировать поступление нового сообщения в очередь.
            /// </summary>
            ValueTask NewMessageInQueueAsync(
                ICollection<KeyDto> keys,
                CancellationToken cancellationToken);

            /// <summary>
            /// Зафиксировать что очередь опустела.
            /// </summary>
            void QueueIsEmpty(
                KeyDto key,
                long timestamp,
                CancellationToken cancellationToken);

            /// <summary>
            /// Ожидание поступления нового сообщения в очередь (все очереди пустые).
            /// </summary>
            Task<Task> AllQueueEmptySleepAsync(
                CancellationToken cancellationToken);

            /// <summary>
            /// !Для тестов.
            /// </summary>
            void Clear();
        }

        public readonly record struct KeyDto(
            string HandlerName,
            short Priority)
        {
            public override int GetHashCode()
            {
                return HashCode.Combine(HandlerName, Priority);
            }
        }
    }
}