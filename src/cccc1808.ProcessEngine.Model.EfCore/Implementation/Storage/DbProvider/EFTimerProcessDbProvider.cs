using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;
using cccc1808.ProcessEngine.Model.Abstract.Common.Entities.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.Common.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Registry;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Dto.Components;
using cccc1808.ProcessEngine.Model.Implementation.Dto.Components;
using cccc1808.ProcessEngine.Model.Implementation.Storage;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Storage.DbProvider
{
    internal class EFTimerProcessDbProvider<TId, TDbContext, TProcessDbEntity, TTimerProcessDbEntity>
        : IProcessDbProvider<TId>
        where TDbContext : DbContext
        where TTimerProcessDbEntity : TimerProcessDbEntity<TId>
        where TProcessDbEntity : ProcessDbEntity<TId>
    {
        protected readonly TDbContext _dbContext;
        protected readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IEnumerable<IProcessDbProvider<TId>> _processLoaders;
        private readonly ReTryTimerProcessRegistryDto _reTryTimerProcessRegistryDto;
        private readonly IId_RangeCondition<TId, TTimerProcessDbEntity> _timer_id_RangeCondition;
        private readonly IId_RangeCondition<TId, TProcessDbEntity> _process_id_RangeCondition;

        public EFTimerProcessDbProvider(
            TDbContext dbContext,
            ILockQueryHintStore lockQueryHintStore,
            IEnumerable<IProcessDbProvider<TId>> processLoaders,
            ReTryTimerProcessRegistryDto reTryTimerProcessRegistryDto)
        {
            _dbContext = dbContext;
            _lockQueryHintStore = lockQueryHintStore;
            _timer_id_RangeCondition = new IId_RangeCondition<TId, TTimerProcessDbEntity>();
            _process_id_RangeCondition = new IId_RangeCondition<TId, TProcessDbEntity>();
            _processLoaders = processLoaders;
            _reTryTimerProcessRegistryDto = reTryTimerProcessRegistryDto;
        }

        public async Task LoadForAsyncProcessingAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            var timers = processes
                .Values
                .Select(e => (e.Process, e.TryGetComponent<ITimerProcessComponent<TId>>(out var timer), Timer: timer))
                .Where(e => e.Item2)
                .Select(e => (e.Process, e.Timer))
                .ToArray();

            Dictionary<TId, TProcessDbEntity> linkedProcesses;
            Dictionary<TId, TTimerProcessDbEntity> linkedTimers;
            using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                var ids = timers
                    .Where(e => e.Timer.LinkedProcessId != null)
                    .GroupBy(e => e.Timer.IsProcessOrTimer);

                linkedProcesses = await _dbContext.Set<TProcessDbEntity>()
                    .ApplayFilterCondition(_process_id_RangeCondition, ids.First(e => e.Key).Select(e => e.Timer.LinkedProcessId).ToArray())
                    .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);
                linkedTimers = await _dbContext.Set<TTimerProcessDbEntity>()
                    .ApplayFilterCondition(_timer_id_RangeCondition, ids.First(e => !e.Key).Select(e => e.Timer.LinkedProcessId).ToArray())
                    .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);
            }

            foreach (var elem in timers)
            {
                if (elem.Timer.LinkedProcessId == null)
                {
                    continue;
                }

                if (elem.Timer.IsProcessOrTimer)
                {
                    if (linkedProcesses.TryGetValue(elem.Timer.LinkedProcessId, out var linkedProcess))
                    {
                        elem.Timer.LinkedProcess = new ProcessContainer<TId>(
                            new EFProcessProxyComponent<TId>(linkedProcess),
                            null
                            );
                        continue;
                    }
                }
                else
                {
                    if (linkedTimers.TryGetValue(elem.Timer.LinkedProcessId, out var linkedProcess))
                    {
                        elem.Timer.LinkedProcess = new ProcessContainer<TId>(
                            new EFProcessProxyComponent<TId>(linkedProcess),
                            null
                            );
                        continue;
                    }
                }

                // TODO: Можно увеличить select timeout для записей, по которым не удалось получить блокировку для основного процесса.
                processes.Remove(elem.Process.Info.Id.Id);
            }
        }

        public async Task UpdateAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            foreach (var elem in processes)
            {
                if (
                    elem.CurrentSession.CreateRetryTimer.HasValue
                    && !elem.CurrentSession.RetryTimerCreated)
                {
                    var timer = new TimerProcessDbEntity<TId>()
                    {
                        //TODO: Id
                        // Id = ,
                        ProcessTypeId = _reTryTimerProcessRegistryDto.ProcessType.ProcessType,
                        ProcessVersion = _reTryTimerProcessRegistryDto.ProcessType.ProcessVersion,
                        LinkedProcessId = elem.Id,
                        LinkedProcess = null,
                        IsProcessOrTimer = true,
                        HaveErrorFlag = false,
                        ReTryCount = null,
                        Priority = default,
                        SelectLock = DateTimeOffset.MinValue.UtcDateTime,
                        TimerDate = elem.CurrentSession.CreateRetryTimer.Value,
                        Error = new ProcessErrorDbEntity<TId>(),
                        Status = ProcessStatusEnum.AsyncExecute,
                    };

                    _dbContext.Add(timer);
                    elem.CurrentSession.RetryTimerCreated = true;
                }
            }

            foreach (var elem in _processLoaders)
            {
                await elem.UpdateAsync(processes, cancellationToken);
            }
        }
    }
}
