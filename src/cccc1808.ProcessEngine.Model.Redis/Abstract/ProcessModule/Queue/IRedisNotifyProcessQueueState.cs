using System.Collections.Immutable;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule.Queue
{
    public interface IRedisNotifyProcessQueueState
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
        }
    }
}