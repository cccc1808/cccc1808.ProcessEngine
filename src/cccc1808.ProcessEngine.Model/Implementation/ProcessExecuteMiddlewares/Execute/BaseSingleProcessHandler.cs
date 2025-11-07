using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Abstract.Storage;
using cccc1808.ProcessEngine.Model.Abstract.Storage.Repository;
using cccc1808.ProcessEngine.Model.Common.Condition;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Execute
{
    /// <summary>
    /// Базовая реализация процессов по одному.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public abstract class BaseSingleProcessHandler<TId> 
        : ExecuteStepByStepGroupMiddleware<TId>.IHandler
    {
        private readonly IIsolationService _isolationService;
        protected readonly IProcessRepository<TId> _repository;
        protected readonly IProcessSetter _processSetter;
        private readonly ProcessInstanceInfoDto_Id_Condition<TId> _processInstanceInfoDto_Id_Condition;

        protected BaseSingleProcessHandler(
            IIsolationService isolationService, 
            IProcessRepository<TId> repository, 
            IProcessSetter processSetter)
        {
            _isolationService = isolationService;
            _repository = repository;
            _processSetter = processSetter;
            _processInstanceInfoDto_Id_Condition = new ProcessInstanceInfoDto_Id_Condition<TId>();
        }

        #region ExecuteStepByStepGroupMiddleware<TId>.IHandler

        public ExecuteStepByStepGroupMiddleware<TId>.OptionsDto Options => SingleOptions.GroupOptions;

        public virtual ValueTask<ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup> GetExecutionGroupAsync(
            IDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> process,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(
                new ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup(
                    string.Empty, 
                    process
                    )
                );
        }

        public virtual async ValueTask<ICollection<IProcessContainer<TId>>> LoadProcessesWithLockSkipLockedRangeAsync(
            IReadOnlyList<ProcessInstanceInfoDto<TId>> ids, 
            CancellationToken cancellationToken)
        {
            var data = await _repository.GetRangeForAsyncProcessingAsync(
                ids.ApplayProjectionCondition(_processInstanceInfoDto_Id_Condition).ToArray(),
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

        public virtual async ValueTask StepRangeAsync(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group, 
            CancellationToken cancellationToken)
        {
            var options = SingleOptions;

            foreach (var elem in group.Group.Values)
            {
                var elemGroup = new ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup(
                    group.Key,
                    new Dictionary<ProcessIdDto<TId>, IProcessContainer<TId>>() { [elem.Process.Info.Id] = elem }
                    );

                await _isolationService.ExecuteAsync(
                    SingleOptions.ProcessIsolation,
                    (This: this, elem, elemGroup, options),
                    static async (p, cancellationToken) =>
                    {
                        await p.This.StepAsync(
                            p.elem,
                            cancellationToken);

                        if (p.options.UseSave)
                        {
                            await p.This.SaveRangeAsync(
                                p.elemGroup,
                                cancellationToken);
                        }
                    },
                    static async (p, ex, cancellationToken) =>
                    {
                        await p.This.OnExceptionRangeAsync(
                            p.elemGroup,
                            ex,
                            cancellationToken);
                    },
                    null,
                    cancellationToken
                    );
            }
        }

        #endregion

        protected abstract OptionsDto SingleOptions { get; }

        protected abstract ValueTask StepAsync(
            IProcessContainer<TId> process,
            CancellationToken cancellationToken);

        public record OptionsDto(
            ExecuteStepByStepGroupMiddleware<TId>.OptionsDto GroupOptions,
            IIsolationService.IsolationMode ProcessIsolation,
            bool UseSave
            );
    }
}
