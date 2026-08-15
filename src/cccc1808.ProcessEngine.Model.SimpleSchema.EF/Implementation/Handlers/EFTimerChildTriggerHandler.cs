using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.CascadeTrigger;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Entity;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Handlers
{
    public class EFTimerChildTriggerHandler<TId>
        : BaseRootChildTriggerRangeHandler<TId>
    {
        public static string Name => "SimpleSchema.ChildTriggerHandler";

        private readonly IEFDbContext _dbContext;
        private readonly TriggerRunner<TId>.OptionsDto _queueOptions;

        public EFTimerChildTriggerHandler(
            ITriggerSetter<TId> triggerSetter,
            ITriggerEventRaiser<TId> triggerEventRaiser,
            IEFDbContext dbContext,
            TriggerRunner<TId>.OptionsDto queueOptions)
            : base(
                  triggerSetter, 
                  triggerEventRaiser)
        {
            _dbContext = dbContext;
            _queueOptions = queueOptions;
        }

        public override async ValueTask<IDictionary<string, ITriggerRangeHandler<TId>.ResultDto>> CheckAsync(
            IEnumerable<ITriggerComponent<TId>> triggers,
            bool isEmergencyTrigger,
            CancellationToken cancellationToken)
        {
            var processData = await _dbContext.Set<SchemaProcessDataDbEntity<TId>>()
                .AsNoTracking()
                .Where(e => triggers.Select(e => e.ProcessId).Contains(e.ProcessId))
                .ToDictionaryAsync(e => e.ProcessId, e => e, cancellationToken);

            return triggers.ToDictionary(
                e => e.Key,
                e => new ITriggerRangeHandler<TId>.ResultDto(
                    ITriggerHandler.ResultDto.RemoveResult(),
                    NeedExecute: true
                    )
                );
        }

        protected override async Task<IDictionary<string, ITriggerHandlerFacade<TId>.RootEventInfoDto>> GetEventInfoAsync(
            IEnumerable<ITriggerComponent<TId>> triggers, 
            CancellationToken cancellationToken)
        {
            var processData = await _dbContext.Set<SchemaProcessDataDbEntity<TId>>()
                .AsNoTracking()
                .Where(e => triggers.Select(e => e.ProcessId).Contains(e.ProcessId))
                .ToDictionaryAsync(e => e.ProcessId, e => e, cancellationToken);

            return triggers.ToDictionary(
                e => e.Key,
                e => new ITriggerHandlerFacade<TId>.RootEventInfoDto(
                    _queueOptions.Consumer_TriggerEventQueues.Single().QueueName,
                    processData[e.ProcessId].RootTriggerKey.ToString())
                );
        }
    }
}
