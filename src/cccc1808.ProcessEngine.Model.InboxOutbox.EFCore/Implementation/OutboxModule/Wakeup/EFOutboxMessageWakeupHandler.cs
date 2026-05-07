using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Handlers;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Conditions;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Wakeup
{
    public class EFOutboxMessageWakeupHandler<TId> : IWakeupCheckHandler<TId>
    {
        private readonly IEFDbContext _dbContext;

        private readonly IProcessLinkedConditions<TId, OutboxMessageDbEntity<TId>> _processLinkedConditions;
        private readonly IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>> _messageStreamConditions;

        public EFOutboxMessageWakeupHandler(
            IEFDbContext dbContext, 
            
            IProcessLinkedConditions<TId, OutboxMessageDbEntity<TId>> processLinkedConditions,
            IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>> messageStreamConditions)
        {
            _dbContext = dbContext;
            _processLinkedConditions = processLinkedConditions;
            _messageStreamConditions = messageStreamConditions;
        }

        public async ValueTask<IDictionary<TId, bool>> HandleRangeAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            var data = await _dbContext
                .Set<OutboxMessageDbEntity<TId>>()
                .ApplayQueryCondition(_processLinkedConditions.ProcessId.QueryRange, processes.Select(e => e.Id).ToArray())
                .ApplayQueryCondition(_messageStreamConditions.IsActiveMessages.Query)
                .GroupBy(e => e.ProcessId)
                .Select(e => new { e.Key, Any = e.Any() })
                .ToDictionaryAsync(e => e.Key, e => e.Any, cancellationToken);

            var result = new Dictionary<TId, bool>(processes.Count);
            foreach (var elem in processes)
            {
                if (data.TryGetValue(elem.Id, out var haveMessage))
                {
                    result.Add(elem.Id, haveMessage);
                }
                else 
                {
                    result.Add(elem.Id, false);
                }
            }
            return result;
        }
    }
}
