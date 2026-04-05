using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Services
{
    public interface IInboxService
    {
        ValueTask ProcessBatchAsync(
            ICollection<MessageDto> batch, 
            CancellationToken cancellationToken);
    }
}