using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Abstract.Storage.Repository;
using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Execute;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Services
{
    /// <summary>
    /// Паттерн для использования на основе EF + ChangeTracker.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    /// <typeparam name="TDbContext"></typeparam>
    public abstract class BaseEFChangeTrackerIJobHandler1<TId>
        : ExecuteJobRangeMiddleware<TId>.IHandler
    {
        protected readonly IProcessRepository<TId> _repository;
        protected readonly IProcessSetter _setter;
        protected readonly ProcessInstanceInfoDto_Id_Condition<TId> _processInstanceInfoDto_Id_Condition;

        protected BaseEFChangeTrackerIJobHandler1(
            IProcessRepository<TId> repository, 
            IProcessSetter setter)
        {
            _repository = repository;
            _setter = setter;
            _processInstanceInfoDto_Id_Condition = new ProcessInstanceInfoDto_Id_Condition<TId>();
        }

        #region ExecuteJobRangeMiddleware

        public ExecuteJobRangeMiddleware<TId>.OptionsDto Options { get; }
            = new ExecuteJobRangeMiddleware<TId>.OptionsDto(
                UseSavepoint: true,
                UseSave: true);

        public virtual async Task<ICollection<IProcessContainer<TId>>> LoadWithLockRangeSkipLockedAsync(
            IReadOnlyList<ProcessInstanceInfoDto<TId>> ids,
            CancellationToken cancellationToken)
        {
            var data = await _repository.GetRangeForAsyncProcessingAsync(
                ids.ApplayProjectionCondition(_processInstanceInfoDto_Id_Condition).ToArray(),
                cancellationToken);

            return data;
        }

        public abstract ValueTask HandleRangeAsync(
            IReadOnlyDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> processes,
            CancellationToken cancellationToken);

        public virtual ValueTask OnExceptionRangeAsync(
            IReadOnlyDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> processes,
            Exception ex,
            CancellationToken cancellationToken)
        {
            foreach (var elem in processes.Values)
            {
                _setter.SetError(elem, ex);
            }

            return ValueTask.CompletedTask;
        }

        public virtual async Task SaveRangeAsync(
            IReadOnlyDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            await _repository.UpdateAsync(
                processes.Values.ToArray(),
                cancellationToken);
        }

        #endregion
    }
}