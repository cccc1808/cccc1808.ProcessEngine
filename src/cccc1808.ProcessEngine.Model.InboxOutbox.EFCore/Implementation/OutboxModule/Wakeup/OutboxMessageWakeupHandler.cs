using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Handlers;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Wakeup
{
    public class OutboxMessageWakeupHandler<TId> : IWakeupCheckHandler<TId>
    {
        private readonly IEFDbContext _dbContext;

        private readonly IProcessLinkedConditions<TId, OutboxMessageDbEntity<TId>> _processLinkedConditions;
        private readonly IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>> _messageStreamConditions;

        public OutboxMessageWakeupHandler(
            IEFDbContext dbContext, 
            
            IProcessLinkedConditions<TId, OutboxMessageDbEntity<TId>> processLinkedConditions,
            IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>> messageStreamConditions)
        {
            _dbContext = dbContext;
            _processLinkedConditions = processLinkedConditions;
            _messageStreamConditions = messageStreamConditions;
        }

        public async ValueTask HandleRangeAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            var result = await _dbContext
                .Set<OutboxMessageDbEntity<TId>>()
                .ApplayQueryCondition(_processLinkedConditions.ProcessId.QueryRange, processes.Select(e => e.Id).ToArray())
                .ApplayQueryCondition(_messageStreamConditions.IsActiveMessages.Query)
                .GroupBy(e => e.ProcessId)
                .Select(e => new { e.Key, Any = e.Any() })
                .ToDictionaryAsync(e => e.Key, e => e.Any, cancellationToken);

            foreach (var elem in processes)
            {
                var component = elem.GetComponent<IWakeUpComponent>();

                if (result.TryGetValue(elem.Id, out var haveMessage))
                {
                    component.HandlerResult = haveMessage;
                }
                else 
                {
                    component.HandlerResult = false;
                }
            }
        }
    }
}
