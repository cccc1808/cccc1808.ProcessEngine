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
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Retry;

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
            var retryTriggers = new List<ITriggerRepository<TId>.CreateTriggerDto>(group.Group.Count);
            foreach (var elem in group.Group.Values)
            {
                var errorResult = _processSetter.SetError(elem, ex, allowRetry: true);

                // Retry trigger.
                if (errorResult.IsRetry)
                {
                    // Если процесс в ошибке, то он не выполняется (в том числе не публекуется событие для стримов),
                    // поэтому можно использовать NoWakeupRetryTriggerRangeHandler.
                    retryTriggers.Add(
                        ITriggerRepository<TId>.CreateTriggerDto.TimerTrigger(
                            key: Guid.NewGuid().ToString(),
                            timerDate: errorResult.Timeout,
                            processId: elem.Id,
                            isRangeTrigger: true,
                            handlerKey: NoWakeupRetryTriggerRangeHandler<Guid>.Name,
                            priority: elem.Process.Info.Priority,
                            isActivated: true,
                            isChildTrigger: false
                            )
                        );

                    //if (elem.WakeupState == WakeupStateEnum.WakeupWithState)
                    //{
                    //    retryTriggers.Add(
                    //        ITriggerRepository<TId>.CreateTriggerDto.TimerTrigger(
                    //            key: Guid.NewGuid().ToString(),
                    //            timerDate: errorResult.Timeout,
                    //            processId: elem.Id,
                    //            handlerKey: WakeupTriggerRangeHandler<TId>.Name,
                    //            priority: elem.Process.Info.Priority,
                    //            isActivated: true,
                    //            counter: null,
                    //            streamState: null));                        
                    //}
                    //else 
                    //{
                    //}                        
                }
            }

            if (retryTriggers.Any())
            {
                await _triggerRepository.CreateTriggerRangeAsync(
                    retryTriggers,
                    cancellationToken);
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
