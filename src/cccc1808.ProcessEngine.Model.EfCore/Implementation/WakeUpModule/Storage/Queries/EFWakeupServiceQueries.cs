using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeUpModule.Storage.Queries
{
    public class EFWakeupServiceQueries<TId>
        : IWakeupServiceQueries<TId>
    {
        private readonly IEFDbContext _dbContext;
        private readonly ILockQueryHintStore _lockQueryHintStore;

        private readonly IProcessContainerConditions<TId> _processContainerConditions;
        private readonly IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> _processDbEntityConditions;
        private readonly IProcessWakeupDbEntityConditions<TId> _processWakeUpDbEntityConditions;

        public EFWakeupServiceQueries(
            IEFDbContext dbContext,
            ILockQueryHintStore lockQueryHintStore,
            
            IProcessContainerConditions<TId> processContainerConditions,
            IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> processDbEntityConditions,
            IProcessWakeupDbEntityConditions<TId> processWakeUpDbEntityConditions)
        {
            _dbContext = dbContext;
            _lockQueryHintStore = lockQueryHintStore;

            _processContainerConditions = processContainerConditions;
            _processDbEntityConditions = processDbEntityConditions;
            _processWakeUpDbEntityConditions = processWakeUpDbEntityConditions;
        }

        public async Task<IDictionary<TId, IWakeupServiceQueries<TId>.WakeupDto>> AfterSession_LoadStateWithLockAsync(
            ICollection<TId> ids,
            CancellationToken cancellationToken)
        {
            // 1) Получаем wakeup с блокировкой
            // (это небходимо, чтобы если сейчас идет попытка пробуждения по новому внешнему сигналу, то она будет выполняться после завершения это транзакции, и новый сигнал не потеряется).
            using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
            {
                var data = await _dbContext.Set<ProcessWakeupDbEntity<TId>>()
                    .AsNoTracking()
                    .ApplayQueryCondition(_processWakeUpDbEntityConditions.ProcessLinkedDbEntity.QueryRange, ids)
                    .OrderBy(e => e.ProcessId) // Info: Для упорядоченной блокировки
                    .ToArrayAsync(cancellationToken);

                return data.ToDictionary(
                    e => e.ProcessId,
                    e => new IWakeupServiceQueries<TId>.WakeupDto(e.Id, e.ProcessId, e.IsAsyncExecuting));
            }
        }        

        public async Task<IDictionary<TId, TId>> Wakeup_LoadStateAsync(
            ICollection<TId> ids,
            TimeSpan wakeupTryUpdatelockTimeout,
            CancellationToken cancellationToken)
        {
            var checkBuffer = ids.ToHashSet();
            var updateBuffer = new Dictionary<TId, ProcessWakeupDbEntity<TId>>(ids.Count);

            while (true)
            {
                //// Замечание: share lock не является обязательно необходимым, может быть достаточной реализация только на основе update lock.

                // 1) Если намерение выставлено - IsAsyncExecuting, то обновлять ничего не нужно, достаточно ShareLock до конца транзакции.
                using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForShare))
                {
                    var wakeups = await _dbContext.Set<ProcessWakeupDbEntity<TId>>()
                        // .AsNoTracking()
                        .ApplayQueryCondition(
                            _processWakeUpDbEntityConditions.ProcessLinkedDbEntity.QueryRange,
                            ids
                            )
                        .ApplayQueryCondition(_processWakeUpDbEntityConditions.IsAsyncExecuting.Query)
                        .OrderBy(e => e.ProcessId) // Info: Для упорядоченной блокировки
                        .Select(e => e.ProcessId)
                        .ToArrayAsync(cancellationToken);

                    foreach (var elem in wakeups)
                    {
                        // Пробуждение не нужно.
                        checkBuffer.Remove(elem);
                    }
                }

                // 2) Получаем updlock.
                var result = await TimeoutHelper.ExecuteWithTimeoutAsync(
                    (This: this, checkBuffer, updateBuffer),
                    wakeupTryUpdatelockTimeout,
                    static async (p, t) =>
                    {
                        using (var _ = p.This._lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                        {
                            var wakeupsWithLock = await p.This._dbContext.Set<ProcessWakeupDbEntity<TId>>()
                                // .AsNoTracking()
                                .ApplayQueryCondition(p.This._processWakeUpDbEntityConditions.ProcessLinkedDbEntity.QueryRange, p.checkBuffer)
                                .OrderBy(e => e.ProcessId) // Info: Для упорядоченной блокировки
                                .ToArrayAsync(t);

                            // У нас монопольная блокировка wakeup через updlock.
                            foreach (var elem in wakeupsWithLock)
                            {
                                if (p.This._processWakeUpDbEntityConditions.IsAsyncExecuting.Memory.Check(elem))
                                {
                                    // Пробуждение не нужно.
                                    p.checkBuffer.Remove(elem.ProcessId);
                                }
                                else
                                {
                                    // Пробуждение нужно.
                                    p.updateBuffer.Add(elem.ProcessId, elem);
                                    p.checkBuffer.Remove(elem.ProcessId);
                                }
                            }
                        }
                    },
                    cancellationToken
                    );

                if (result)
                {
                    break;
                }
            }

            return updateBuffer.ToDictionary(
                e => e.Value.ProcessId,
                e => e.Value.Id);
        }

        public async Task<ICollection<IWakeupServiceQueries<TId>.ProcessInfoDto>> Wakeup_LoadProcessesAsync(
            ICollection<TId> ids,
            CancellationToken cancellationToken)
        {
            // Процессы не в состоянии обработки т.к. мы получили updatelock на wakeup и увидели статус WaitEvent.
            ProcessDbEntity<TId>[] processes;
            using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
            {
                processes = await _dbContext.Set<ProcessDbEntity<TId>>()
                    // .AsNoTracking()
                    .ApplayQueryCondition(_processDbEntityConditions.Id.QueryRange, ids)
                    .ToArrayAsync(cancellationToken);

                return processes
                    .Select(e => new IWakeupServiceQueries<TId>.ProcessInfoDto(
                        e.Id, 
                        e.StoppedByError, 
                        e.RetryCount,
                        e.Status))
                    .ToArray();
            }
        }

        public Task Wakeup_ExecuteAsync(
            ICollection<IWakeupServiceQueries<TId>.WakeupDto> data,
            CancellationToken cancellationToken)
        {
            // Загружены в вышестоящих методах, берем из ChangeTracker.
            var wakeupEntityDictionary = _dbContext.DbContext.ChangeTracker
                .Entries<ProcessWakeupDbEntity<TId>>()
                .ToDictionary(e => e.Entity.ProcessId, e => e.Entity);

            var trackedProcessesDictionary = _dbContext.DbContext.ChangeTracker
                .Entries<ProcessDbEntity<TId>>()
                .ToDictionary(e => e.Entity.Id, e => e.Entity);

            var set = _dbContext.Set<ProcessWakeupDbEntity<TId>>();
            foreach (var elem in data)
            {
                if (elem.IsAsyncExecuting)
                {
                    var process = trackedProcessesDictionary[elem.ProcessId];
                    process.Status = ProcessStatusEnum.AsyncExecute;

                    var wakeup = wakeupEntityDictionary[elem.ProcessId];
                    wakeup.IsAsyncExecuting = true;
                }
            }

            return Task.CompletedTask;
        }
    }
}
