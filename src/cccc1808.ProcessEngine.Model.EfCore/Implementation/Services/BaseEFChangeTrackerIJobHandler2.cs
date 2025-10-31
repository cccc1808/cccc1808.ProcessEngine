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
using cccc1808.ProcessEngine.Model.Common;
using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Storage;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Execute;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Services
{
    /// <summary>
    /// Паттерн для использования на основе EF + ChangeTracker.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    /// <typeparam name="TDbContext"></typeparam>
    public abstract class BaseEFChangeTrackerIJobHandler2<TId>
        : ExecuteJobRangeMiddleware<TId>.IHandler
    {
        private readonly IChangeTrackerSnapshotService _changeTrackerSnapshotService;
        protected readonly IProcessRepository<TId> _repository;
        protected readonly IProcessSetter _setter;
        protected readonly ProcessInstanceInfoDto_Id_Condition<TId> _processInstanceInfoDto_Id_Condition;

        protected BaseEFChangeTrackerIJobHandler2(
            IChangeTrackerSnapshotService changeTrackerSnapshotService,
            IProcessRepository<TId> repository, 
            IProcessSetter setter)
        {
            _changeTrackerSnapshotService = changeTrackerSnapshotService;
            _repository = repository;
            _setter = setter;
            _processInstanceInfoDto_Id_Condition = new ProcessInstanceInfoDto_Id_Condition<TId>();
        }

        #region ExecuteJobRangeMiddleware

        public ExecuteJobRangeMiddleware<TId>.OptionsDto Options { get; }
            = new ExecuteJobRangeMiddleware<TId>.OptionsDto(
                UseSavepoint: false,
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

        public virtual async ValueTask HandleRangeAsync(
            IReadOnlyDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            if (processes.Count == 1)
            {
                // Здесь можно писать в БД напрямую т.к. процессы нарезаны по 1 на savepoint (save).

                await HandleAsync(processes.Values.First(), cancellationToken);
            }
            else 
            {
                foreach (var elem in processes.Values)
                {
                    // Здесь не рекомендуется писать в БД напрямую т.к. в случае Exception нет никакого сброка БД.

                    var changeTrackerSnapshot = _changeTrackerSnapshotService.CaptureState();

                    try
                    {
                        await HandleAsync(elem, cancellationToken);
                        changeTrackerSnapshot.NoRestore();
                    }
                    catch (Exception ex)
                    {
                        if (OperationCancelHelper.IsCancelException(ex, cancellationToken))
                        {
                            changeTrackerSnapshot.NoRestore();
                            throw;
                        }

                        changeTrackerSnapshot.Restore();
                        await OnExceptionRangeAsync(
                            new Dictionary<ProcessIdDto<TId>, IProcessContainer<TId>>() { [elem.Process.Info.Id] = elem },
                            ex,
                            cancellationToken);
                    }
                }
            }
        }

        public virtual ValueTask OnExceptionRangeAsync(
            IReadOnlyDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> processes,
            Exception ex,
            CancellationToken cancellationToken)
        {
            foreach (var elem in processes.Values)
            {
                _setter.SetError(elem, ex, allowRetry: true);
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

        protected abstract ValueTask HandleAsync(
            IProcessContainer<TId> process,
            CancellationToken cancellationToken);

        public async Task SaveWakeupRangeAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            await _repository.UpdateWakeupAsync(
                processes,
                cancellationToken);
        }
    }
}