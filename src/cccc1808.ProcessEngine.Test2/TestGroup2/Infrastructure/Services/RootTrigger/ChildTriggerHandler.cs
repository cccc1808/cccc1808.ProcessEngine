using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Base;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services.RootTrigger
{
    internal class ChildTriggerHandler
        : BaseRootChildTriggerRangeHandler<Guid>
    {
        public static string Name => "TestGroup2.RootTrigger.ChildTriggerHandler";

        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEFDbContext _dbContext;
        private readonly TriggerRunner<Guid>.OptionsDto _queueOptions;

        public ChildTriggerHandler(
            ITriggerSetter<Guid> triggerSetter,
            ITriggerEventRaiser<Guid> triggerEventRaiser,
            IDateTimeProvider dateTimeProvider,
            IEFDbContext dbContext,
            TriggerRunner<Guid>.OptionsDto queueOptions)
            : base(
                  triggerSetter, 
                  triggerEventRaiser)
        {
            _dateTimeProvider = dateTimeProvider;
            _dbContext = dbContext;
            _queueOptions = queueOptions;
        }

        public override async ValueTask<IDictionary<string, ITriggerRangeHandler<Guid>.ResultDto>> CheckAsync(
            IEnumerable<ITriggerComponent<Guid>> triggers,
            bool isEmergencyTrigger,
            CancellationToken cancellationToken)
        {
            var processData = await _dbContext.Set<RootTriggerDbEntity>()
                .AsNoTracking()
                .Where(e => triggers.Select(e => e.ProcessId).Contains(e.ProcessId))
                .ToDictionaryAsync(e => e.ProcessId, e => e, cancellationToken);

            return triggers.ToDictionary(
                e => e.Key,
                e => new ITriggerRangeHandler<Guid>.ResultDto(
                    ITriggerHandler.ResultDto.NoActivateResult(_dateTimeProvider.UtcNow),
                    NeedExecute: !processData[e.ProcessId].IsFirst
                    )
                );
        }

        protected override async Task<IDictionary<string, ITriggerHandlerFacade<Guid>.RootEventInfoDto>> GetEventInfoAsync(
            IEnumerable<ITriggerComponent<Guid>> triggers, 
            CancellationToken cancellationToken)
        {
            var processData = await _dbContext.Set<RootTriggerDbEntity>()
                .AsNoTracking()
                .Where(e => triggers.Select(e => e.ProcessId).Contains(e.ProcessId))
                .ToDictionaryAsync(e => e.ProcessId, e => e, cancellationToken);

            return triggers.ToDictionary(
                e => e.Key,
                e => new ITriggerHandlerFacade<Guid>.RootEventInfoDto(
                    _queueOptions.TriggerEventQueues.Single().QueueName,
                    processData[e.ProcessId].RootTriggerId.ToString())
                );
        }
    }
}
