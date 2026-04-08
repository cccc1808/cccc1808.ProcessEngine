using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.ClassifierModule.Dto;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Storage
{
    public interface IClassifierRepository<TId>
    {
        ValueTask<long> GetOutboxOrderIdAsync(
            (AggregateDto Aggreagate, string Queue) aggregate, 
            CancellationToken cancellationToken);

        ValueTask<long> GetInboxOrderIdAsync(
            (AggregateDto Aggreagate, string Queue) aggregate,
            CancellationToken cancellationToken);

        /// <summary>
        /// (TId ProcessId, TId AggregateId, TId QueueId) by AggragateId
        /// </summary>
        /// <param name="aggregates"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<IDictionary<(AggregateDto Aggreagate, string Queue), (TId ProcessId, TId QueueId, string Queue, string TriggerKey)>> GetInboxInfoAsync(
            ICollection<(AggregateDto Aggreagate, string Queue)> info,
            CancellationToken cancellationToken);

        /// <summary>
        /// OutboxProcessId by AggreagateDto
        /// </summary>
        /// <param name="aggregates"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<IDictionary<(AggregateDto Aggreagate, string Queue), (TId ProcessId, TId QueueId, string Queue, string TriggerKey)>> GetOutboxInfoAsync(
            ICollection<(AggregateDto Aggreagate, string Queue)> aggregates,
            CancellationToken cancellationToken);
    }
}
