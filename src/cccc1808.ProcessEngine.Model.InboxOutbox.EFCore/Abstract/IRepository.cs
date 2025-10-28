using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract
{
    public interface IInboxOutboxRepository<TId>
    {
        ValueTask<TId> GetOrCreateQueueIdAsync(
            string name,
            CancellationToken cancellationToken);

        ValueTask<IDictionary<string, TId>> GetOrCreateAggregateIdRangeAsync(
            ICollection<string> name,
            CancellationToken cancellationToken
            );

        ValueTask<IDictionary<TId, TId>> GetOrCreateInboxStreamByAggregateIdAsync(
            ICollection<TId> aggregateIds
            );

        ValueTask<IDictionary<TId, TId>> GetOrCreateOutboxStreamByAggregateIdAsync(
            ICollection<TId> aggregateIds
            );

        ValueTask SendMessagesAsync(
            IDictionary<TId, ICollection<MessageDto>> messagesByStreams,
            CancellationToken cancellationToken);
    }
}
