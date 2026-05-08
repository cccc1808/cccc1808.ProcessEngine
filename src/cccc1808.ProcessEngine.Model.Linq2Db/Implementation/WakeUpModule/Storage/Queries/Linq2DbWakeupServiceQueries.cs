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
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.WakeupModule.Storage.Qeury;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.WakeupModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;

using LinqToDB;
using LinqToDB.Async;
using LinqToDB.DataProvider.PostgreSQL;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.WakeUpModule.Storage.Queries
{
    public class Linq2DbWakeupServiceQueries<TId>
        : IWakeupServiceQueries<TId>
    {
        private readonly ILinq2DbDataConnection _linq2DbDataConnection;

        private readonly IProcessContainerConditions<TId> _processContainerConditions;
        private readonly IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> _processDbEntityConditions;
        private readonly IProcessWakeupDbEntityConditions<TId> _processWakeUpDbEntityConditions;

        public Linq2DbWakeupServiceQueries(
            ILinq2DbDataConnection linq2DbDataConnection,

            IProcessContainerConditions<TId> processContainerConditions,
            IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> processDbEntityConditions,
            IProcessWakeupDbEntityConditions<TId> processWakeUpDbEntityConditions
            )
        {
            _linq2DbDataConnection = linq2DbDataConnection;

            _processContainerConditions = processContainerConditions;
            _processDbEntityConditions = processDbEntityConditions;
            _processWakeUpDbEntityConditions = processWakeUpDbEntityConditions;
        }

        public async Task<IDictionary<TId, IWakeupServiceQueries<TId>.IWakeupInfoDto>> AfterSession_LoadStateWithLockAsync(
            ICollection<TId> ids,
            CancellationToken cancellationToken)
        {
            // 1) Получаем wakeup с блокировкой
            // (это небходимо, чтобы если сейчас идет попытка пробуждения по новому внешнему сигналу, то она будет выполняться после завершения это транзакции, и новый сигнал не потеряется).
            var data = await _linq2DbDataConnection.Set<ProcessWakeupDbEntity<TId>>()                
                .ApplayQueryCondition(_processWakeUpDbEntityConditions.ProcessLinkedDbEntity.QueryRange, ids)
                .QueryHint(PostgresQueryHint.ForNoKeyUpdate)
                .OrderBy(e => e.ProcessId) // Info: Для упорядоченной блокировки                
                .ToArrayAsync(cancellationToken);

            return data.ToDictionary(
                e => e.ProcessId,
                e => (IWakeupServiceQueries<TId>.IWakeupInfoDto)new WakeupServiceQueries<TId>.WakeupInfoDto(
                    e.Id, 
                    e.ProcessId, 
                    e.IsAsyncExecuting));
        }

        public async Task<IWakeupServiceQueries<TId>.IWakeupContext> Wakeup_LoadStateAsync(
            ICollection<TId> ids,
            bool useShareLock, 
            TimeSpan wakeupTryUpdatelockTimeout,
            CancellationToken cancellationToken)
        {
            var updateBuffer = new Dictionary<TId, IWakeupServiceQueries<TId>.IContextEntryDto>(ids.Count);

            if (useShareLock)
            {
                var checkBuffer = ids.ToHashSet();

                while (true)
                {
                    //// Замечание: share lock не является обязательно необходимым, может быть достаточной реализация только на основе update lock.

                    // 1) Если намерение выставлено - IsAsyncExecuting, то обновлять ничего не нужно, достаточно ShareLock до конца транзакции.                    
                    {
                        var wakeups = await _linq2DbDataConnection.Set<ProcessWakeupDbEntity<TId>>()
                            .ApplayQueryCondition(
                                _processWakeUpDbEntityConditions.ProcessLinkedDbEntity.QueryRange,
                                ids
                                )
                            .ApplayQueryCondition(_processWakeUpDbEntityConditions.IsAsyncExecuting.Query)
                            .QueryHint(PostgresQueryHint.ForShare)
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
                            var wakeupsWithLock = await p.This._linq2DbDataConnection.Set<ProcessWakeupDbEntity<TId>>()
                                .ApplayQueryCondition(p.This._processWakeUpDbEntityConditions.ProcessLinkedDbEntity.QueryRange, p.checkBuffer)
                                .QueryHint(PostgresQueryHint.ForNoKeyUpdate)
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
                                    p.updateBuffer.Add(
                                        elem.ProcessId, 
                                        new WakeupServiceQueries<TId>.ContextEntryDto(
                                            (elem.Id, elem.ProcessId, elem.IsAsyncExecuting),
                                            null)
                                        );
                                    p.checkBuffer.Remove(elem.ProcessId);
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
            }
            else
            {
                {
                    var wakeupsWithLock = await _linq2DbDataConnection.Set<ProcessWakeupDbEntity<TId>>()
                        .ApplayQueryCondition(_processWakeUpDbEntityConditions.ProcessLinkedDbEntity.QueryRange, ids)
                        .QueryHint(PostgresQueryHint.ForNoKeyUpdate)
                        .OrderBy(e => e.ProcessId) // Info: Для упорядоченной блокировки
                        .ToArrayAsync(cancellationToken);

                    // У нас монопольная блокировка wakeup через updlock.
                    foreach (var elem in wakeupsWithLock)
                    {
                        if (_processWakeUpDbEntityConditions.IsAsyncExecuting.Memory.Check(elem))
                        {
                            // Пробуждение не нужно.
                        }
                        else
                        {
                            // Пробуждение нужно.
                            updateBuffer.Add(
                                elem.ProcessId,
                                new WakeupServiceQueries<TId>.ContextEntryDto(
                                    (elem.Id, elem.ProcessId, elem.IsAsyncExecuting),
                                    null));
                        }
                    }
                }
            }

            return new WakeupServiceQueries<TId>.WakeupContext(updateBuffer);
        }

        public async Task Wakeup_LoadProcessesWithLockAsync(
            IWakeupServiceQueries<TId>.IWakeupContext context,
            CancellationToken cancellationToken)
        {
            // Процессы не в состоянии обработки т.к. мы получили updatelock на wakeup и увидели статус WaitEvent.
            var processes = await _linq2DbDataConnection.Set<ProcessDbEntity<TId>>()
                .ApplayQueryCondition(_processDbEntityConditions.Id.QueryRange, context.Data.Keys)
                .QueryHint(PostgresQueryHint.ForNoKeyUpdate)
                .Select(e => new { e.Id, e.StoppedByError, e.RetryCount, e.Status })
                .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

            foreach (var elem in context.Data.Values)
            {
                if (elem is not WakeupServiceQueries<TId>.ContextEntryDto entry)
                {
                    throw new ArgumentException($"[Bug]. Ожидается {typeof(WakeupServiceQueries<TId>.ContextEntryDto)}.");
                }

                var process = processes[elem.WakeupState.ProcessId];
                entry.ProcessState = (process.StoppedByError, process.RetryCount, process.Status);
            }
        }

        public async Task Wakeup_ExecuteAsync(
            IWakeupServiceQueries<TId>.IWakeupContext context,
            CancellationToken cancellationToken)
        {
            await _linq2DbDataConnection.Set<ProcessDbEntity<TId>>()
                .Where(e => context.ToWakeupData.Select(e => e.WakeupState.ProcessId).Contains(e.Id))
                .Set(e => e.Status, ProcessStatusEnum.AsyncExecute)
                .UpdateAsync(cancellationToken);

            await _linq2DbDataConnection.Set<ProcessWakeupDbEntity<TId>>()
                .Where(e => context.ToWakeupData.Select(e => e.WakeupState.Id).Contains(e.Id))
                .Set(e => e.IsAsyncExecuting, true)
                .UpdateAsync(cancellationToken);
        }
    }
}
