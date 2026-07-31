using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services
{
    /// <summary>
    /// <see cref="ParentCheckWakeupHandler"/>.
    /// </summary>
    internal class ParentProcessTriggerHandler
        : ITriggerRangeHandler<Guid>
    {
        public const string Name = "ParentProcessTriggerHandler";

        private readonly IEFDbContext _dbContext;
        private readonly ITriggerHandlerFacade<Guid> _triggerHandlerFacade;

        public ParentProcessTriggerHandler(
            IEFDbContext dbContext,
            ITriggerHandlerFacade<Guid> triggerHandlerFacade)
        {
            _dbContext = dbContext;
            _triggerHandlerFacade = triggerHandlerFacade;
        }

        public async ValueTask<IDictionary<string, ITriggerRangeHandler<Guid>.ResultDto>> CheckAsync(
            IEnumerable<ITriggerComponent<Guid>> triggers, 
            bool isEmergencyTrigger, 
            CancellationToken cancellationToken)
        {
            var haveNotCompleteChilds = await _dbContext
                .Set<ChildProcessDbEntity>()
                .Where(e => triggers.Select(e => (Guid?)e.ProcessId).Contains(e.ActiveParentProcessId))
                .GroupBy(e => e.ActiveParentProcessId)
                .Select(e => new { e.Key, Count = e.Count() })
                .ToDictionaryAsync(e => e.Key, e => e, cancellationToken);

            return triggers.ToDictionary(
                e => e.Key, 
                e => haveNotCompleteChilds.TryGetValue(e.ProcessId, out var exists) && exists.Count > 0
                // Есть незавершенные дочерние процессы.
                ? new ITriggerRangeHandler<Guid>.ResultDto(
                    // Не активирован - активируется когда придет хотя бы одно событие от ребенка
                    ITriggerHandler.ResultDto.NoActivateResult(
                        // Задержка, чтобы триггер активировался не по первому сообщению (если незавершенных процессов много)
                        // формулу можно уточнить.
                        DateTimeOffset.Now + TimeSpan.FromSeconds(1) * exists.Count),
                    NeedExecute: false)
                // Все дочерние процессы завершились.
                : new ITriggerRangeHandler<Guid>.ResultDto(
                    ITriggerHandler.ResultDto.RemoveResult(),
                    NeedExecute: true
                    )
                );
        }

        public async ValueTask ExecuteAsync(
            IEnumerable<ITriggerComponent<Guid>> triggers, 
            CancellationToken cancellationToken)
        {
            await _triggerHandlerFacade.ToAsyncExecutingWakeupAsync(
                triggers.Select(e => e.ProcessId).ToArray(), 
                cancellationToken);
        }
    }
}
