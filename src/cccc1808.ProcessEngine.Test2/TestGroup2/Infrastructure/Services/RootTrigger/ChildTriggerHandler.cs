using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services.RootTrigger
{
    internal class ChildTriggerHandler
        : BaseRootChildTriggerRangeHandler<Guid>
    {
        public static string Name => "TestGroup2.RootTrigger.ChildTriggerHandler";

        private readonly IEFDbContext _dbContext;
        private readonly TriggerRunner<Guid>.OptionsDto _queueOptions;

        public ChildTriggerHandler(
            ITriggerEventRaiser<Guid> eventRaiser,
            IEFDbContext dbContext,
            TriggerRunner<Guid>.OptionsDto queueOptions)
            : base(eventRaiser)
        {
            _dbContext = dbContext;
            _queueOptions = queueOptions;
        }

        protected override async ValueTask<IDictionary<string, ResultDto>> CheckAsync(
            IEnumerable<ITriggerComponent<Guid>> triggers,
            CancellationToken cancellationToken)
        {
            var processData = await _dbContext.Set<RootTriggerDbEntity>()
                .AsNoTracking()
                .Where(e => triggers.Select(e => e.ProcessId).Contains(e.ProcessId))
                .ToDictionaryAsync(e => e.ProcessId, e => e, cancellationToken);

            return triggers.ToDictionary(
                e => e.Key,
                e => new ResultDto(
                    new Model.Abstract.TriggerModule.Handlers.ITriggerHandler.Result(), 
                    e.ProcessId,
                    NeedSignal: !processData[e.ProcessId].IsFirst,
                    RootTriggerKey: processData[e.ProcessId].RootTriggerId.ToString(),
                    RootTriggerQueueName: _queueOptions.TriggerEventQueues.Single().QueueName)
                );
        }
    }
}
