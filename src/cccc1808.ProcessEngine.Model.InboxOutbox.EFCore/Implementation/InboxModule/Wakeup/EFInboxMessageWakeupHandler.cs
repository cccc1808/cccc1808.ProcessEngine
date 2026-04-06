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
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Wakeup
{
    public class EFInboxMessageWakeupHandler<TId> : IWakeupCheckHandler<TId>
    {
        private readonly IEFDbContext _dbContext;

        private readonly IProcessLinkedConditions<TId, InboxMessageDbEntity<TId>> _processLinkedConditions;
        private readonly IMessageStreamConditions<TId, InboxMessageDbEntity<TId>> _messageStreamConditions;

        public EFInboxMessageWakeupHandler(
            IEFDbContext dbContext,
            
            IProcessLinkedConditions<TId, InboxMessageDbEntity<TId>> processLinkedConditions,
            IMessageStreamConditions<TId, InboxMessageDbEntity<TId>> messageStreamConditions)
        {
            _dbContext = dbContext;
            _processLinkedConditions = processLinkedConditions;
            _messageStreamConditions = messageStreamConditions;
        }

        public async ValueTask HandleRangeAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            var result = await _dbContext.Set<InboxMessageDbEntity<TId>>()
                .ApplayQueryCondition(
                    _processLinkedConditions.ProcessId.QueryRange,
                    processes.Select(e => e.Id).ToArray()
                    )
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
