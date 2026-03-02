using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Abstract.Storage.Repository;
using cccc1808.ProcessEngine.Model.Common.Condition;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Execute
{
    public abstract class BaseRangeProcessHandler<TId>
        : ExecuteStepByStepGroupMiddleware<TId>.IHandler
    {
        protected readonly IProcessRepository<TId> _repository;
        protected readonly IProcessSetter _processSetter;

        protected BaseRangeProcessHandler(
            IProcessRepository<TId> repository,
            IProcessSetter processSetter)
        {
            _repository = repository;
            _processSetter = processSetter;
        }

        #region ExecuteStepByStepGroupMiddleware<TId>.IHandler

        public abstract ExecuteStepByStepGroupMiddleware<TId>.OptionsDto Options { get; }

        public virtual ValueTask<ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup> GetExecutionGroupAsync(
            IDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> process,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(
                new ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup(process)
                );
        }

        public virtual async ValueTask<ICollection<IProcessContainer<TId>>> LoadProcessesWithLockSkipLockedRangeAsync(
            IReadOnlyList<ProcessInstanceInfoDto<TId>> ids,
            CancellationToken cancellationToken)
        {
            var data = await _repository.GetRangeForAsyncProcessingAsync(
                ids.Select(e => e.Id).ToArray(),
                cancellationToken);

            return data;
        }

        public virtual ValueTask OnExceptionRangeAsync(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group,
            Exception ex,
            CancellationToken cancellationToken)
        {
            foreach (var elem in group.Group.Values)
            {
                _processSetter.SetError(elem, ex, allowRetry: true);
            }
            return ValueTask.CompletedTask;
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
