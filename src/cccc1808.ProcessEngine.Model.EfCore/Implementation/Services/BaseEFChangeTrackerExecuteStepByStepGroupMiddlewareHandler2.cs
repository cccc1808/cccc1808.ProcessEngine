using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Execute;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Services
{
    /// <summary>
    /// Реализация 2.
    /// Использует сохранение после каждого шага.
    /// Самый медленный вариант.
    /// Для этого режима имеет смысл разбивать на батчи по 1 элементу и запускать каждый в отдельной транзакции параллельно.
    /// </summary>
    public abstract class BaseEFChangeTrackerExecuteStepByStepGroupMiddlewareHandler2<TId>
        : ExecuteStepByStepGroupMiddleware<TId>.IHandler
    {
        private readonly IIsolationService _isolationService;
        protected readonly IProcessRepository<TId> _repository;
        protected readonly IProcessSetter _processSetter;
        private readonly ProcessInstanceInfoDto_Id_Condition<TId> _processInstanceInfoDto_Id_Condition;

        protected BaseEFChangeTrackerExecuteStepByStepGroupMiddlewareHandler2(
            IIsolationService isolationService,
            IProcessRepository<TId> repository, 
            IProcessSetter processSetter)
        {
            _isolationService = isolationService;   
            _repository = repository;
            _processSetter = processSetter;
            _processInstanceInfoDto_Id_Condition = new ProcessInstanceInfoDto_Id_Condition<TId>();
        }

        #region ExecuteStepByStepGroupMiddleware<TId, TContext>.IHandler

        public ExecuteStepByStepGroupMiddleware<TId>.OptionsDto Options 
            => new ExecuteStepByStepGroupMiddleware<TId>.OptionsDto(
                CycleLimit: 50,
                UseSavepoint: false,
                UseAfterGroupSave: true,
                UseEndSave: false);

        public virtual ValueTask<ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup> GetExecutionGroupAsync(
            IDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> process,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(
                new ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup(
                    "",
                    process.Take(1).ToDictionary()                
                    )
                );
        }

        public async ValueTask<ICollection<IProcessContainer<TId>>> LoadProcessesWithLockSkipLockedRangeAsync(
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
                _processSetter.SetError(elem, ex);
            }
            return ValueTask.CompletedTask;
        }

        public virtual async Task SaveRangeAsync(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group,
            CancellationToken cancellationToken)
        {
            await _repository.UpdateAsync(
                group.Group.Values.ToArray(),
                cancellationToken);
        }

        public virtual async ValueTask<ICollection<ProcessIdDto<TId>>> StepRangeAsync(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group,
            CancellationToken cancellationToken)
        {
            var complete = new List<ProcessIdDto<TId>>(group.Group.Count);

            foreach (var elem in group.Group.Values)
            {
                await _isolationService.ExecuteAsync(                    
                    group.Group.Count == 1
                    ? IIsolationService.IsolationMode.No // Если элемент один, то мы дополнительно ничего не нужно.
                    : IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker,
                    (This: this, elem,),
                    static async (p, cancellationToken) =>
                    {
                        var result = await p.This.StepAsync(
                            p.elem,
                            cancellationToken);

                        if (!result)
                        {
                            complete.Add(p.elem.Process.Info.Id);
                        }
                    },
                    static async (p, ex, cancellationToken) =>
                    {
                        await p.This.OnExceptionRangeAsync(
                            new ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup(
                                p.group.Key,
                                new Dictionary<ProcessIdDto<TId>, IProcessContainer<TId>>() { [elem.Process.Info.Id] = elem }
                                ),
                            ex,
                            cancellationToken);
                    },
                    null,
                    cancellationToken);
            }

            return complete;
        }

        #endregion

        protected abstract ValueTask<bool> StepAsync(
            IProcessContainer<TId> process,
            CancellationToken cancellationToken);
    }
}
