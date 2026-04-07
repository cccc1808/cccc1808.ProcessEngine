using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Services
{
    public interface IInboxConsumerService
    {
        ValueTask ProcessBatchAsync(
            ICollection<MessageDto> batch, 
            CancellationToken cancellationToken);
    }
}