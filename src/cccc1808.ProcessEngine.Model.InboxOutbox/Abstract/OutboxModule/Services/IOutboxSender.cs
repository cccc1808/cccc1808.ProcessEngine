using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.ClassifierModule.Dto;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Services
{
    /// <summary>
    /// Публикация событий в TransactionOutbox.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IOutboxSender<TId>
    {
        ValueTask SendAsync(
            ICollection<(AggregateDto aggregate, MessageDto message)> messages,
            CancellationToken cancellationToken);
    }
}
