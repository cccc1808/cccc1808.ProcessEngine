using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Common;
using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.Common.Entities.Conditions;
using cccc1808.ProcessEngine.Model.Common.QueryHint;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities.Conditions;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Services
{
    public class EFWakeUpService<TId, TDbContext> 
        : IWakeUpService<TId> 
        where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;
        private readonly IProcessSetter _processSetter;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IId_RangeCondition<TId, ProcessDbEntity<TId>> _process_id_RangeCondition;
        private readonly IId_RangeCondition<TId, WakeUpProcessDbEntity<TId>> _w_id_RangeCondition;
        private readonly WakeUpProcessDbEntity_IsAsyncExecuting_Condition<TId> _wakeUpProcessDbEntity_IsAsyncExecuting_Condition;
        private readonly WakeUpProcessDbEntity_IsAsyncExecuting_TimerDate_RangeCondition<TId> _wakeUpProcessDbEntity_IsAsyncExecuting_TimerDate_RangeCondition;
        private readonly TimeSpan _wakeupEndUpdLockTimeout = TimeSpan.FromSeconds(2);
        private readonly TimeSpan _sessionEndUpdLockTimeout = TimeSpan.FromSeconds(2);
        private readonly int _wakeupUpdLockRetryLimit = 2;


        public EFWakeUpService(
            TDbContext dbContext,
            IProcessSetter processSetter,
            ILockQueryHintStore lockQueryHintStore)
        {
            _dbContext = dbContext;
            _processSetter = processSetter;
            _lockQueryHintStore = lockQueryHintStore;
            _process_id_RangeCondition = new IId_RangeCondition<TId, ProcessDbEntity<TId>>();
            _w_id_RangeCondition = new IId_RangeCondition<TId, WakeUpProcessDbEntity<TId>>();
            _wakeUpProcessDbEntity_IsAsyncExecuting_Condition = new WakeUpProcessDbEntity_IsAsyncExecuting_Condition<TId>();
            _wakeUpProcessDbEntity_IsAsyncExecuting_TimerDate_RangeCondition = new WakeUpProcessDbEntity_IsAsyncExecuting_TimerDate_RangeCondition<TId>();
        }

        #region IWakeUpService

        public async Task AfterAsyncSessionHandlerAsync(
            ICollection<IProcessContainer<TId>> processes,
            // Func<ICollection<IProcessContainer<TId>>, CancellationToken, ValueTask<ICollection<(TId, bool)>>> checkWakeUp,
            Func<ICollection<IProcessContainer<TId>>, CancellationToken, ValueTask> saveHandler,
            CancellationToken cancellationToken)
        {
            // Для засыпающих процессов, что поступление сигнала на пробуждение.
            var context = processes
                .Where(e => e.Process.Status == ProcessStatusEnum.WaitEvent) // Засыпает (Если только засыпает, можно подумать еще про вариации AsyncExecite и TimerDate)
                .Where(e => !e.Process.HaveErrorFlag && !e.CurrentSession.HaveError) // Не ошибки
                .Select(e => (e, e.TryGetComponent<IWakeUpComponent>(out var component), component)) // Есть компонент
                .Where(e => e.Item2)
                .ToDictionary(
                    e => e.e.Id,
                    e => new ExecuteContextItemDto()
                    {
                        Process = e.e,
                        WakeUpComponent = e.component,
                    }
                    );

            {
                // Блокировка используется, чтобы не допустить ситуации, когда другая транзакция попытается пробудить процесс,
                // а мы это не увидим (и процесс уснент)
                // (ждем завершения блокировок всех сигналов).

                WakeUpProcessDbEntity<TId>[]? wakeUps = null;

                // Пробуем получить все записи с блокировкой.
                using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                {
                    var result = await TimeoutHelper.ExecuteWithTimeoutAsync(
                        (_dbContext, _w_id_RangeCondition, context),
                        _sessionEndUpdLockTimeout,
                        static async (p, t) =>
                        {
                            return await p._dbContext.Set<WakeUpProcessDbEntity<TId>>()
                                .AsNoTracking()
                                .ApplayFilterCondition(p._w_id_RangeCondition, p.context.Keys)
                                .ToArrayAsync(t);
                        },
                        cancellationToken);

                    if (!result.IsTimeout)
                    {
                        wakeUps = result.Result;
                    }
                }                
                
                if (wakeUps == null)
                {
                    // Пробуем загрузить то, что не заблокировано.
                    using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
                    {
                        wakeUps = await _dbContext.Set<WakeUpProcessDbEntity<TId>>()
                            .AsNoTracking()
                            .ApplayFilterCondition(_w_id_RangeCondition, context.Keys)
                            .ToArrayAsync(cancellationToken);
                    }
                }

                foreach (var elem in wakeUps)
                {
                    context[elem.Id].WakeupWithLock = elem;
                }

                // Проверяем улосвие необходимости пробуждения.
                //var needWakeUpResult = await checkWakeUp(
                //    context.Values.Select(e => e.Process).ToArray(),
                //    cancellationToken);
                //foreach (var elem in needWakeUpResult)
                //{
                //    context[elem.Item1].NeedWakeUp = elem.Item2;
                //}
                // Для упрощения уберу проверку условия.
                foreach (var elem in context.Values)
                {
                    elem.NeedWakeUp = true;
                }
            }

            foreach (var elem in context.Values)
            {
                if (elem.WakeupWithLock is null)
                {
                    // Если не получили блокировку, то не засыпаем.
                    _processSetter.SetStatus(elem.Process, ProcessStatusEnum.AsyncExecute);
                    elem.WakeUpComponent.NeedUpdate = false; // Не получили блокирову, не пытаемся сохранить.

                    // Ставим задержку т.к. процесс хотел уснуть. Чтобы он дождался уменьшения частоты блокировки из-за сигнала.
                    elem.Process.Process.WakeupLockCounter++;
                    // TODO: в параметры.
                    elem.Process.Process.TimerDate = DateTimeOffsetHelper.Max(
                        elem.Process.Process.TimerDate, 
                        DateTimeOffset.UtcNow + elem.Process.Process.WakeupLockCounter * TimeSpan.FromSeconds(10)
                        );

                    continue;
                }

                elem.Process.Process.WakeupLockCounter = 0;
                elem.WakeUpComponent.NeedUpdate = true;

                if (elem.NeedWakeUp)
                {
                    // Процесс убедился в необходимости пробуждения.

                    DateTimeOffset nextTimerDate;
                    if (elem.WakeUpComponent.SessionStartTimeStamp == elem.WakeupWithLock.TimeStamp)
                    {
                        // Если дата не менялась с начала обработки, значит новых внешних сигналов пробуждения не было.   
                        nextTimerDate = elem.Process.Process.TimerDate;
                    }
                    else
                    {
                        // Поступал новый внешний сигнал пробуждения, берем минимальную задержку таймера.
                        nextTimerDate = DateTimeOffsetHelper.Min(elem.Process.Process.TimerDate, elem.WakeupWithLock.TimerDate);
                    }

                    _processSetter.SetStatus(elem.Process, ProcessStatusEnum.AsyncExecute);
                    _processSetter.SetTimer(elem.Process, nextTimerDate);
                    elem.WakeUpComponent.TimerDate = nextTimerDate;
                }
                else
                {
                    // Процесс не увидел необходимости пробуждения.

                    elem.WakeUpComponent.Timestamp = DateTimeOffset.UtcNow;
                    elem.WakeUpComponent.TimerDate = elem.Process.Process.TimerDate;
                    elem.WakeUpComponent.IsAsyncExecuting = false;
                }
            }

            await saveHandler(
                context.Select(e => e.Value.Process).ToArray(),
                cancellationToken);
        }

        public async Task WakeUpProcessHandlerAsync(
            (TId Id, DateTimeOffset? delayMinDate)[] data,
            CancellationToken cancellationToken)
        {
            var grouppedData = data
                .GroupBy(e => e.delayMinDate.HasValue)
                .ToArray();

            // Не обновляем меняем дату
            await WakeUpWithoutDateAsync(
                grouppedData.First(e => !e.Key).Select(e => e.Id).ToArray(),
                cancellationToken);

            // Обновляем дату, если передана меньше текущей.
            await WakeUpWithDateAsync(
                grouppedData.First(e => e.Key).Select(e => (e.Id, e.delayMinDate.Value)).ToArray(),
                cancellationToken);
        }

        #endregion

        private async Task WakeUpWithDateAsync(
            (TId Id, DateTimeOffset delayMinDate)[] data,
            CancellationToken cancellationToken)
        {
            if (data.Length == 0)
            {
                return;
            }

            var checkBuffer = data.ToDictionary(e => e.Id, e => e.delayMinDate);
            var updateBuffer = new Dictionary<TId, (DateTimeOffset delayMinDate, WakeUpProcessDbEntity<TId> Wakeup)>(data.Length);

            // Делаем попытки убедиться, что либо share lock если условие выполняется, либо updlock если не выполняется.
            
            for (int i = 0; i < _wakeupUpdLockRetryLimit; i++)
            {
                // 1) Если StreamActiveFlag, то обновлять ничего не нужно, достаточно ShareLock до конца транзакции.
                using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForShare))
                {
                    var wakeups = await _dbContext.Set<WakeUpProcessDbEntity<TId>>()
                        .AsNoTracking()
                        .ApplayFilterCondition(
                            _wakeUpProcessDbEntity_IsAsyncExecuting_TimerDate_RangeCondition,
                            (
                                _dbContext,
                                checkBuffer.Select(e => (e.Key, e.Value)).ToArray()
                            )
                            )
                        .Select(e => e.Id)
                        .ToArrayAsync(cancellationToken);

                    // Флаг взведен и дата меньше.
                    foreach (var elem in wakeups)
                    {
                        // Пробуждение не нужно.
                        checkBuffer.Remove(elem);
                    }
                }

                // 2) Пробуем получить updlock.
                var result = await TimeoutHelper.ExecuteWithTimeoutAsync(
                    (This: this, checkBuffer, updateBuffer),
                    _wakeupEndUpdLockTimeout,
                    static async (p, t) =>
                    {
                        WakeUpProcessDbEntity<TId>[] wakeupsWithLock;
                        using (var _ = p.This._lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                        {
                            wakeupsWithLock = await p.This._dbContext.Set<WakeUpProcessDbEntity<TId>>()
                                .AsNoTracking()
                                .ApplayFilterCondition(p.This._w_id_RangeCondition, p.checkBuffer.Keys)
                                .ToArrayAsync(t);
                        }

                        // Блокировка получена.
                        foreach (var elem in wakeupsWithLock)
                        {
                            var checkDate = p.checkBuffer[elem.Id];

                            if (p.This._wakeUpProcessDbEntity_IsAsyncExecuting_TimerDate_RangeCondition.Check(elem, checkDate))
                            {
                                // Кто-то уже обновил, Пробуждение не нужно.
                                p.checkBuffer.Remove(elem.Id);
                            }
                            else
                            {
                                // Пробуждение нужно.
                                p.updateBuffer.Add(elem.Id, (checkDate, elem));
                                p.checkBuffer.Remove(elem.Id);
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

            // Если не получилось (высокая конкрунция с другим сигналом), то не будем обновлять дату, а проверим только IsAsyncExecute.
            if (checkBuffer.Count != 0)
            {
                await WakeUpWithoutDateAsync(
                    checkBuffer.Keys, 
                    cancellationToken);
            }

            // 3) Обновляем wakeup и process
            {
                var processIsActiveGroups = updateBuffer
                    .Select(e => e.Value.Wakeup)
                    .GroupBy(e => e.IsAsyncExecuting)
                    .ToArray();

                foreach (var elem in updateBuffer.Values)
                {
                    elem.Wakeup.TimeStamp = DateTimeOffset.UtcNow;
                    elem.Wakeup.IsAsyncExecuting = true;
                    elem.Wakeup.TimerDate = elem.delayMinDate;
                }

                {
                    // Процесс !IsAsyncExecuting, значит он гарантировано не заблокирован, активируем и указываем дату.
                    ProcessDbEntity<TId>[] processes;
                    using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                    {
                        processes = await _dbContext.Set<ProcessDbEntity<TId>>()
                            .AsNoTracking()
                            .ApplayFilterCondition(
                                _process_id_RangeCondition,
                                processIsActiveGroups.First(e => !e.Key).Select(e => e.Id).ToArray())
                            // .OrderBy(e => e.Id) // Для упорядочивания блокировки
                            .ToArrayAsync(cancellationToken);
                    }

                    foreach (var elem in processes)
                    {
                        if (elem.HaveErrorFlag || elem.ReTryCount.HasValue) // TODO: condition
                        {
                            // Если стрим упал в ошибку, то не трогаем его.
                            continue;
                        }

                        elem.Status = ProcessStatusEnum.AsyncExecute;
                        elem.TimerDate = DateTimeOffsetHelper.Min(elem.TimerDate, updateBuffer[elem.Id].delayMinDate);
                    }
                }

                {
                    // Процесс IsAsyncExecuting (может исполняться), поэтому обновляем только если не заблокирован.
                    // Если стрим исполняется, то нашу дату он увидит в конце через wakeup entity за счет изменения TimeStamp.
                    ProcessDbEntity<TId>[] streams;
                    using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
                    {
                        streams = await _dbContext.Set<ProcessDbEntity<TId>>()
                            .AsNoTracking()
                            .ApplayFilterCondition(
                                _process_id_RangeCondition,
                                processIsActiveGroups.First(e => e.Key).Select(e => e.Id).ToArray())
                            .ToArrayAsync(cancellationToken);
                    }

                    foreach (var elem in streams)
                    {
                        if (elem.HaveErrorFlag) // TODO: condition
                        {
                            // Если стрим упал в ошибку, то не трогаем его.
                            continue;
                        }

                        elem.Status = ProcessStatusEnum.AsyncExecute;
                        elem.TimerDate = DateTimeOffsetHelper.Min(elem.TimerDate, updateBuffer[elem.Id].delayMinDate);
                    }
                }
            }
        }

        private async Task WakeUpWithoutDateAsync(
            ICollection<TId> data,
            CancellationToken cancellationToken)
        {
            if (data.Count == 0)
            {
                return;
            }

            var checkBuffer = data.ToHashSet();
            var updateBuffer = new Dictionary<TId, WakeUpProcessDbEntity<TId>>(data.Count);

            while (true) 
            {
                // 1) Если намерение выставлено - IsAsyncExecuting, то обновлять ничего не нужно, достаточно ShareLock до конца транзакции.
                using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForShare))
                {
                    var wakeups = await _dbContext.Set<WakeUpProcessDbEntity<TId>>()
                        .AsNoTracking()
                        .ApplayFilterCondition(
                            _w_id_RangeCondition,
                            data
                            )
                        .ApplayFilterCondition(_wakeUpProcessDbEntity_IsAsyncExecuting_Condition, default)
                        .Select(e => e.Id)
                        .ToArrayAsync(cancellationToken);

                    foreach (var elem in wakeups)
                    {
                        // Пробуждение не нужно.
                        checkBuffer.Remove(elem);
                    }
                }

                // 2) Пробуем получить UpdateLock.
                var result = await TimeoutHelper.ExecuteWithTimeoutAsync(
                    (This: this, checkBuffer, updateBuffer),
                    _wakeupEndUpdLockTimeout,
                    static async (p,t) => 
                    {
                        using (var _ = p.This._lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                        {
                            var wakeupsWithLock = await p.This._dbContext.Set<WakeUpProcessDbEntity<TId>>()
                                .AsNoTracking()
                                .ApplayFilterCondition(p.This._w_id_RangeCondition, p.checkBuffer)
                                .ToArrayAsync(t);

                            // У нас монопольная блокировка через updlock.
                            foreach (var elem in wakeupsWithLock)
                            {
                                if (p.This._wakeUpProcessDbEntity_IsAsyncExecuting_Condition.Check(elem, default))
                                {
                                    // Пробуждение не нужно.
                                    p.checkBuffer.Remove(elem.Id);
                                }
                                else
                                {
                                    // Пробуждение нужно.
                                    p.updateBuffer.Add(elem.Id, elem);
                                    p.checkBuffer.Remove(elem.Id);
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

            // Обновляем wakeup и process
            {
                foreach (var elem in updateBuffer.Values)
                {
                    elem.TimeStamp = DateTimeOffset.UtcNow;
                    elem.IsAsyncExecuting = true;
                }

                // Процессы не обрабатываются т.к. мы получили блокировку на wakeup, котрый в состоянии !IsAsyncExecuting.
                ProcessDbEntity<TId>[] processes;
                using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                {
                    processes = await _dbContext.Set<ProcessDbEntity<TId>>()
                        .ApplayFilterCondition(_process_id_RangeCondition, updateBuffer.Keys)
                        .ToArrayAsync(cancellationToken);
                }

                foreach (var elem in processes)
                {
                    if (elem.HaveErrorFlag || elem.ReTryCount.HasValue) // TODO: condition
                    {
                        // Если стрим упал в ошибку, то не трогаем его.
                        continue;
                    }

                    elem.Status = ProcessStatusEnum.AsyncExecute;
                    // elem.TimerDate = DateTimeOffset.MinValue.UtcDateTime;
                }
            }
        }


        private class ExecuteContextItemDto
        {
            public IProcessContainer<TId> Process { get; init; }

            public IWakeUpComponent WakeUpComponent { get; init; }

            public WakeUpProcessDbEntity<TId>? WakeupWithLock { get; set; }

            public bool NeedWakeUp { get; set; }
        }
    }
}
