using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Wakeup
{
    public class EFOutboxTriggerWakeupHandler<TId>
        : NoWakeupStreamTriggerRangeHandler<TId>
    {
        public new const string Name = "EFOutboxTriggerWakeupHandler";

        private readonly IEFDbContext _dbContext;

        private readonly IProcessLinkedConditions<TId, OutboxMessageDbEntity<TId>> _processLinkedConditions;
        private readonly IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>> _messageStreamConditions;

        public EFOutboxTriggerWakeupHandler(
            IEFDbContext dbContext,
            IProcessRepository<TId> processRepository,
            IProcessSetter processSetter,

            IProcessLinkedConditions<TId, OutboxMessageDbEntity<TId>> processLinkedConditions,
            IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>> messageStreamConditions)
            : base(
                  processRepository,
                  processSetter)
        {
            _dbContext = dbContext;
            _processLinkedConditions = processLinkedConditions;
            _messageStreamConditions = messageStreamConditions;
        }

        public override async ValueTask<IDictionary<string, ITriggerHandler.Result>> HandleAsync(
            IEnumerable<ITriggerComponent<TId>> triggers, 
            CancellationToken cancellationToken)
        {
            // 1) Проверяем наличие нерпочитанных сообщений.
            var data = await _dbContext
                .Set<OutboxMessageDbEntity<TId>>()
                .ApplayQueryCondition(_processLinkedConditions.ProcessId.QueryRange, triggers.Select(e => e.ProcessId).ToArray())
                .ApplayQueryCondition(_messageStreamConditions.IsActiveMessages.Query)
                .GroupBy(e => e.ProcessId)
                .Select(e => new { e.Key, Any = e.Any() })
                .ToDictionaryAsync(e => e.Key, e => e.Any, cancellationToken);

            var forWakeup = new List<ITriggerComponent<TId>>();
            var result = new Dictionary<string, ITriggerHandler.Result>();
            foreach (var elem in triggers)
            {
                var exsist = data.TryGetValue(elem.ProcessId, out var dbExsist) ? dbExsist : false; 

                if (exsist)
                {
                    forWakeup.Add(elem);
                    result.Add(
                        elem.Key,
                        new ITriggerHandler.Result(NeedRepeat: true, IsActivated: false, ExecuteDelay: DateTimeOffset.MinValue));
                }
                else 
                {
                    result.Add(
                        elem.Key, 
                        new ITriggerHandler.Result(NeedRepeat: true, IsActivated: false, ExecuteDelay: DateTimeOffset.MinValue));
                }
            }

            // 2) Пробуждаем процессы.
            await base.HandleAsync(forWakeup, cancellationToken);

            return result;
        }
    }
}
