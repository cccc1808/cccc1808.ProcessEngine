using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Services
{
    public interface IEFInboxService
    {
        ValueTask ProcessBatchAsync(
            ICollection<MessageDto> batch, 
            CancellationToken cancellationToken);
    }
}