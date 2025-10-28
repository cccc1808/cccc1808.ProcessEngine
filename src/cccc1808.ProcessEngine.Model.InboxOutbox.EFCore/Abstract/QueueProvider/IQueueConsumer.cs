using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.QueueProvider
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
