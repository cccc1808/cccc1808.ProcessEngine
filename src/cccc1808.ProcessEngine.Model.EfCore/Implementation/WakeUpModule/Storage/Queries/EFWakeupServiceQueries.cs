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

        public async Task<IDictionary<TId, IWakeupServiceQueries<TId>.IWakeupInfoDto>> AfterSession_LoadStateWithLockAsync(
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
                    e => (IWakeupServiceQueries<TId>.IWakeupInfoDto)new WakeupInfoDto(e.Id, e.ProcessId, e.IsAsyncExecuting));
            }
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
                    //// Оптимизирует блокировку от нескольких триггеров (первый триггер берет updatelock и пробуждает процесс, последующие получают sharelock праллельно и ничего не обновляют)
                    // 1) Если намерение выставлено - IsAsyncExecuting, то процесс выполняется и обновлять ничего не нужно, достаточно ShareLock до конца транзакции.
                    using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForShare))
                    {
                        var wakeups = await _dbContext.Set<ProcessWakeupDbEntity<TId>>()
                            .AsNoTracking()
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
                                    .AsNoTracking()
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
                                        p.updateBuffer.Add(elem.ProcessId, new EFProxyContextEntryDto(elem));
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
            }
            else 
            {
                using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                {
                    var wakeupsWithLock = await _dbContext.Set<ProcessWakeupDbEntity<TId>>()
                        .AsNoTracking()
                        .ApplayQueryCondition(_processWakeUpDbEntityConditions.ProcessLinkedDbEntity.QueryRange, ids)
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
                            updateBuffer.Add(elem.ProcessId, new EFProxyContextEntryDto(elem));
                        }
                    }
                }
            }
            
            return new EFWakeupContext(updateBuffer);
        }

        public async Task Wakeup_LoadProcessesWithLockAsync(
            IWakeupServiceQueries<TId>.IWakeupContext context,
            CancellationToken cancellationToken)
        {
            // Процессы не в состоянии обработки т.к. мы получили updatelock на wakeup и увидели статус WaitEvent.
            using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
            {
                var processes = await _dbContext.Set<ProcessDbEntity<TId>>()
                    .AsNoTracking()
                    .ApplayQueryCondition(_processDbEntityConditions.Id.QueryRange, context.Data.Keys)
                    .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

                foreach (var elem in context.Data.Values)
                {
                    if (elem is not EFProxyContextEntryDto entryProxy)
                    {
                        throw new ArgumentException($"[Bug]. Ожидается {typeof(EFProxyContextEntryDto)}.");
                    }

                    entryProxy.ProcessEntity = processes[elem.WakeupState.ProcessId];
                }
            }
        }

        public Task Wakeup_ExecuteAsync(
            IWakeupServiceQueries<TId>.IWakeupContext context,
            CancellationToken cancellationToken)
        {
            foreach (var elem in context.ToWakeupData)
            {
                if (elem is not EFProxyContextEntryDto entryProxy)
                {
                    throw new ArgumentException($"[Bug]. Ожидается {typeof(EFProxyContextEntryDto)}.");
                }

                if (entryProxy.ProcessEntity is null)
                {
                    throw new ArgumentException($"[Bug]. Процесс должен быть загружен на шаге {nameof(Wakeup_LoadProcessesWithLockAsync)}.");
                }

                _dbContext.AttachEntity(entryProxy.WakeupEntity, throwIfAttached: false);
                _dbContext.AttachEntity(entryProxy.ProcessEntity, throwIfAttached: false);

                entryProxy.WakeupEntity.IsAsyncExecuting = true;
                entryProxy.ProcessEntity.Status = ProcessStatusEnum.AsyncExecute;
            }

            return Task.CompletedTask;
        }

        public record WakeupInfoDto : IWakeupServiceQueries<TId>.IWakeupInfoDto
        {
            public TId Id { get; }

            public TId ProcessId { get; }

            public bool IsAsyncExecuting { get; }

            public WakeupInfoDto(
                TId id, 
                TId processId, 
                bool isAsyncExecuting)
            {
                Id = id;
                ProcessId = processId;
                IsAsyncExecuting = isAsyncExecuting;
            }
        }

        public class EFWakeupContext 
            : IWakeupServiceQueries<TId>.IWakeupContext
        {
            public IDictionary<TId, IWakeupServiceQueries<TId>.IContextEntryDto> Data { get; }

            public ICollection<IWakeupServiceQueries<TId>.IContextEntryDto> ToWakeupData { get; }

            public EFWakeupContext(
                IDictionary<TId, IWakeupServiceQueries<TId>.IContextEntryDto> data)
            {
                Data = data;
                ToWakeupData = new List<IWakeupServiceQueries<TId>.IContextEntryDto>(data.Count);
            }
        }

        public class EFProxyContextEntryDto
            : IWakeupServiceQueries<TId>.IContextEntryDto
        {
            public ProcessWakeupDbEntity<TId> WakeupEntity { get; }

            public ProcessDbEntity<TId>? ProcessEntity { get; internal set; }

            public (TId Id, TId ProcessId, bool IsAsyncExecuting) WakeupState 
                => (WakeupEntity.Id, WakeupEntity.ProcessId, WakeupEntity.IsAsyncExecuting);

            public (bool StoppedByError, short? RetryCount, ProcessStatusEnum Status)? ProcessState 
                => ProcessEntity != null 
                ? (ProcessEntity.StoppedByError, ProcessEntity.RetryCount, ProcessEntity.Status)
                : null;

            public EFProxyContextEntryDto(
                ProcessWakeupDbEntity<TId> wakeupEntity)
            {
                WakeupEntity = wakeupEntity;
            }
        }
    }
}
