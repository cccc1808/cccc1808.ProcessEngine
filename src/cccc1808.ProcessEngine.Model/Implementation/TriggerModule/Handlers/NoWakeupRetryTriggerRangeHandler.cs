using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers
{
    public class NoWakeupRetryTriggerRangeHandler<TId>
        : ITriggerRangeHandler<TId>
    {
        public const string Name = "NoWakeupRetryTriggerRangeHandler";

        private readonly IProcessRepository<TId> _processRepository;
        private readonly IProcessSetter _processSetter;

        public NoWakeupRetryTriggerRangeHandler(
            IProcessRepository<TId> processRepository,
            IProcessSetter processSetter)
        {
            _processRepository = processRepository;
            _processSetter = processSetter;
        }

        public async ValueTask<IDictionary<string, ITriggerHandler.Result>> HandleAsync(
            IEnumerable<ITriggerComponent<TId>> triggers, 
            CancellationToken cancellationToken)
        {
            var processes = await _processRepository.GetWaitingRangeAsync(
                triggers.Select(e => e.ProcessId).ToArray(), 
                updateLock: true, 
                cancellationToken);

            foreach (var elem in processes)
            {
                _processSetter.SetStatus(
                    elem, 
                    Abstract.ProcessModule.Dto.ProcessStatusEnum.AsyncExecute);
            }

            await _processRepository.UpdateAsync(processes, cancellationToken);

            return triggers.ToDictionary(
                e => e.Key, 
                e => new ITriggerHandler.Result(false, false, DateTimeOffset.MinValue));
        }
    }
}
