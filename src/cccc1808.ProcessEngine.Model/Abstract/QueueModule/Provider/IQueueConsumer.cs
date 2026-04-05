using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider
{
    public interface IQueueConsumer 
    {
        ValueTask<ICollection<MessageDto>> ConsumeBatchAsync(
            int limit,
            TimeSpan timeout,
            CancellationToken cancellationToken
            );

        ValueTask CommitAsync(
            CancellationToken cancellationToken);
    }
}
