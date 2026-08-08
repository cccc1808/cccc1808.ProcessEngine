using System.Collections.Immutable;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule.Queue
{
    /// <summary>
    /// Состояние для работы очереди процессов.
    /// Хранит:
    /// 1) Есть ли хотя бы одна непустая очередь (или Task асинхронного ожидания).
    /// 2) Какие очереди предположительно не пустые.
    /// </summary>
    public interface IRedisProcessQueueNotifyState
    {
        IHandler RangeHandler { get; }

        IHandler SingleHandler { get; }

        public interface IHandler
        {
            ImmutableSortedDictionary<ProcessTypeUniqueDto, long> GetQueueWithMessages();

            ValueTask NewMessageInQueueAsync(
                ICollection<ProcessTypeUniqueDto> keys,
                CancellationToken cancellationToken);

            void QueueIsEmpty(
                ProcessTypeUniqueDto key,
                long timestamp,
                CancellationToken cancellationToken);

            Task<Task> AllQueueEmptySleepAsync(
                CancellationToken cancellationToken);

            /// <summary>
            /// !Для тестов.
            /// </summary>
            void Clear();
        }
    }
}