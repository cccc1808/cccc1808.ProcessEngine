using System.Collections.Immutable;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;

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
            ImmutableSortedDictionary<TriggerTypeUniqueDto, long> GetQueueWithMessages();

            /// <summary>
            /// Зафиксировать поступление нового сообщения в очередь.
            /// </summary>
            ValueTask NewMessageInQueueAsync(
                ICollection<TriggerTypeUniqueDto> keys,
                CancellationToken cancellationToken);

            /// <summary>
            /// Зафиксировать что очередь опустела.
            /// </summary>
            void QueueIsEmpty(
                TriggerTypeUniqueDto key,
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
    }
}