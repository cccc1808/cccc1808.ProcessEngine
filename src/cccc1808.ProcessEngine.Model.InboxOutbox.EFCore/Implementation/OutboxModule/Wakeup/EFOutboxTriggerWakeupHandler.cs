using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Stream;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
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
            IDateTimeProvider dateTimeProvider,
            ITriggerHandlerFacade<TId> triggerHandlerFacade,
            IEFDbContext dbContext,            

            IProcessLinkedConditions<TId, OutboxMessageDbEntity<TId>> processLinkedConditions,
            IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>> messageStreamConditions)
            : base(dateTimeProvider, triggerHandlerFacade)
        {
            _dbContext = dbContext;
            _processLinkedConditions = processLinkedConditions;
            _messageStreamConditions = messageStreamConditions;
        }

        public override async ValueTask<IDictionary<string, ITriggerRangeHandler<TId>.ResultDto>> CheckAsync(
            IEnumerable<ITriggerComponent<TId>> triggers,
            bool isEmergencyTrigger,
            CancellationToken cancellationToken)
        {
            var dictionary = triggers.ToDictionary(e => e.Key, e => e);

            // 1) Проверяем наличие нерпочитанных сообщений.
            var data = await _dbContext
                .Set<OutboxMessageDbEntity<TId>>()
                .ApplayQueryCondition(_processLinkedConditions.ProcessId.QueryRange, triggers.Select(e => e.ProcessId).ToArray())
                .ApplayQueryCondition(_messageStreamConditions.IsActiveMessages.Query)
                .GroupBy(e => e.ProcessId)
                .Select(e => new { e.Key, Any = e.Any() })
                .ToDictionaryAsync(e => e.Key, e => e.Any, cancellationToken);

            var result = new Dictionary<string, ITriggerRangeHandler<TId>.ResultDto>();
            foreach (var elem in triggers)
            {
                var exsist = data.TryGetValue(elem.ProcessId, out var dbExsist) ? dbExsist : false;

                if (exsist)
                {
                }
                else
                {
                    dictionary.Remove(elem.Key);
                    result.Add(
                        elem.Key,
                        new ITriggerRangeHandler<TId>.ResultDto(
                            ITriggerHandler.ResultDto.NoActivateResult(),
                            NeedExecute: false)
                        );
                }
            }

            var baseResult = await base.CheckAsync(dictionary.Values, isEmergencyTrigger, cancellationToken);

            foreach (var elem in baseResult)
            {
                result.Add(elem.Key, elem.Value);
            }

            return result;

        }
    }
}
