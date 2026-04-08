using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute
{
    public abstract class BaseRangeProcessHandler<TId>
        : ExecuteStepByStepGroupMiddleware<TId>.IHandler
    {
        protected readonly IProcessRepository<TId> _repository;
        protected readonly ITriggerRepository<TId> _triggerRepository;
        protected readonly IProcessSetter _processSetter;

        protected BaseRangeProcessHandler(
            IProcessRepository<TId> repository,
            ITriggerRepository<TId> triggerRepository,
            IProcessSetter processSetter)
        {
            _repository = repository;
            _triggerRepository = triggerRepository;
            _processSetter = processSetter;
        }

        #region ExecuteStepByStepGroupMiddleware<TId>.IHandler

        public abstract ExecuteStepByStepGroupMiddleware<TId>.OptionsDto Options { get; }

        public virtual ValueTask<ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup> GetExecutionGroupAsync(
            IDictionary<TId, IProcessContainer<TId>> process,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(
                new ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup(process)
                );
        }

        public virtual async ValueTask<ICollection<IProcessContainer<TId>>> LoadProcessesWithLockSkipLockedRangeAsync(
            ICollection<ProcessInstanceInfoDto<TId>> ids,
            CancellationToken cancellationToken)
        {
            var data = await _repository.GetForAsyncProcessingRangeAsync(
                ids,
                cancellationToken);

            return data;
        }

        public virtual async ValueTask OnExceptionRangeAsync(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group,
            Exception ex,
            CancellationToken cancellationToken)
        {
            foreach (var elem in group.Group.Values)
            {
                var errorResult = _processSetter.SetError(elem, ex, allowRetry: true);

                if (errorResult.IsRetry)
                {
                    // Retry trigger.

                    if (elem.TryGetComponent<IWakeupComponent>(out _))
                    {
                        await _triggerRepository.CreateTriggerAsync(
                            key: Guid.NewGuid().ToString(),
                            timerDate: errorResult.Timeout,
                            processId: elem.Id,
                            handlerKey: WakeupTriggerRangeHandler<TId>.Name,
                            kind: Model.Abstract.TriggerModule.Components.ITriggerComponent<TId>.TriggerKind.Timer,
                            priority: elem.Process.Info.Priority,
                            isActivated: true,
                            counter: null,
                            cancellationToken);
                    }
                    else 
                    {
                        await _triggerRepository.CreateTriggerAsync(
                            key: Guid.NewGuid().ToString(),
                            timerDate: errorResult.Timeout,
                            processId: elem.Id,
                            handlerKey: NoWakeupRetryTriggerRangeHandler<Guid>.Name,
                            kind: Model.Abstract.TriggerModule.Components.ITriggerComponent<TId>.TriggerKind.Timer,
                            priority: elem.Process.Info.Priority,
                            isActivated: true,
                            counter: null,
                            cancellationToken);                        
                    }                        
                }
            }
        }

        public virtual async Task SaveRangeAsync(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group,
            CancellationToken cancellationToken)
        {
            await _repository.UpdateAsync(
                group.Group.Values,
                cancellationToken);
        }

        public virtual async Task SaveWakeupRangeAsync(
            ICollection<IProcessContainer<TId>> process,
            CancellationToken cancellationToken)
        {
            await _repository.UpdateWakeupAsync(
                process,
                cancellationToken);
        }

        public abstract ValueTask StepRangeAsync(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group,
            CancellationToken cancellationToken);

        #endregion
    }
}
