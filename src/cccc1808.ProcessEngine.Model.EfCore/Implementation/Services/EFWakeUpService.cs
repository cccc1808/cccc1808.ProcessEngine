using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Common;
using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.Common.QueryHint;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Storage;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Services
{
    public class EFWakeUpService<TId> 
        : IWakeUpService<TId>
    {
        private readonly IEFDbContext _dbContext;
        private readonly IProcessSetter _processSetter;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> _processDbEntityConditions;
        private readonly IProcessWakeUpDbEntityConditions<TId> _processWakeUpDbEntityConditions;
        private readonly OptionsDto _optionsDto;

        public EFWakeUpService(
            IEFDbContext dbContext,
            IProcessSetter processSetter,
            ILockQueryHintStore lockQueryHintStore,
            IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> processDbEntityConditions,
            IProcessWakeUpDbEntityConditions<TId> processWakeUpDbEntityConditions,
            OptionsDto optionsDto)
        {
            _dbContext = dbContext;
            _processSetter = processSetter;
            _lockQueryHintStore = lockQueryHintStore;
            _processDbEntityConditions = processDbEntityConditions;
            _processWakeUpDbEntityConditions = processWakeUpDbEntityConditions;
            _optionsDto = optionsDto;
        }

        #region IWakeUpService

        public async Task AfterAsyncSessionHandlerAsync(
            ICollection<IProcessContainer<TId>> processes,
            Func<ICollection<IProcessContainer<TId>>, CancellationToken, ValueTask> saveHandler,
            CancellationToken cancellationToken)
        {
            var context = new Dictionary<TId, ExecuteContextItemDto>(processes.Count);

            foreach (var elem in processes)
            {
                // Игнорируем процессы с ошибкой.
                if (elem.Process.HaveErrorFlag || elem.CurrentSession.HaveError)
                {
                    continue;
                }

                // Нет компонента.
                if (!elem.TryGetComponent<IWakeUpComponent>(out var component))
                {
                    continue;
                }

                // Флаг - что мы вышли из части ассинхронного выполнения.
                if (!component.InAsyncExecuting)
                {
                    throw new InvalidOperationException("Состояние.");
                }
                component.InAsyncExecuting = false;

                // Обрабатываем только указанные статусы.
                if (elem.Process.Status 
                    is ProcessStatusEnum.AsyncExecute 
                    or ProcessStatusEnum.WaitEvent)
                {
                    context.Add(
                        elem.Id,
                        new ExecuteContextItemDto()
                        {
                            Process = elem,
                            WakeUpComponent = component,
                            WakeupWithLock = null
                        });
                }
            }

            if (context.Count == 0)
            {
                return;
            }

            {
                // Блокировка используется, чтобы не допустить ситуации, когда другая транзакция попытается пробудить процесс,
                // а мы это не увидим (и процесс уснет)
                // (ждем завершения блокировок всех сигналов).

                // Пробуем получить все записи с блокировкой.
                ProcessWakeUpDbEntity<TId>[]? wakeUps = null;
                {
                    var result = await TimeoutHelper.ExecuteWithTimeoutAsync(
                        (This: this, context),
                        _optionsDto.SessionEndUpdLockTimeout,
                        static async (p, t) =>
                        {
                            using (var hint = p.This._lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                            {
                                return await p.This._dbContext.Set<ProcessWakeUpDbEntity<TId>>()
                                    .AsNoTracking()
                                    .ApplayFilterCondition(p.This._processWakeUpDbEntityConditions.ProcessLinkedDbEntity.QueryRange, p.context.Keys)
                                    .ToArrayAsync(t);
                            }
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
                        wakeUps = await _dbContext.Set<ProcessWakeUpDbEntity<TId>>()
                            .AsNoTracking()
                            .ApplayFilterCondition(_processWakeUpDbEntityConditions.ProcessLinkedDbEntity.QueryRange, context.Keys)
                            .ToArrayAsync(cancellationToken);
                    }
                }

                foreach (var elem in wakeUps)
                {
                    context[elem.ProcessId].WakeupWithLock = elem;
                }

                // Можно реализовать кастомную проверку условия, после взятия блокировки (как это было в оригинальнйо системе),
                // но пока не усложняю этот момент.
            }

            foreach (var elem in context.Values)
            {
                // Мы не получили блокировку, это значит что
                if (elem.WakeupWithLock is null)
                {                    
                    // 1) Мы не можем обновить состояние Wakeup в БД.
                    // 2) Мы не видим, уазано ли там меньшее значение таймера.
                    // 3) Идет интенсивная запись сигнала Wakeup, значит засыпать нам не нужно.

                    if (elem.Process.Process.Status == ProcessStatusEnum.WaitEvent)
                    {
                        // Не засыпаем.
                        elem.WakeUpComponent.InAsyncExecuting = true;
                        _processSetter.SetStatus(elem.Process, ProcessStatusEnum.AsyncExecute);

                        // Ставим задержку т.к. процесс хотел уснуть
                        // (Либо все имеющиеся данные были обработаны, либо намерение накопить батч побольше перед обработкой).
                        // Увеличиваем шанс дождатся уменьшения частоты блокировки из-за сигнала.
                        elem.Process.Process.WakeupLockCounter++;
                        
                        // TODO: в параметры.
                        elem.Process.Process.TimerDate = DateTimeOffsetHelper.Max(
                            elem.Process.Process.TimerDate,
                            DateTimeOffset.UtcNow + elem.Process.Process.WakeupLockCounter * _optionsDto.ProcessCannotLockWakeupTimeout
                            );
                    }
                    else
                    {
                        // elem.WakeUpComponent.NeedUpdate = false; // Не получили блокирову, не сохраняем.

                        // Здесь мы просто не обновим timestamp и timer.
                    }

                    continue;
                }

                // Получили блокировку.
                elem.Process.Process.WakeupLockCounter = 0;
                elem.WakeUpComponent.NeedUpdate = true;

                if (elem.WakeUpComponent.SessionStartTimeStamp == elem.WakeupWithLock.TimeStamp)
                {
                    // Если дата не менялась с начала обработки, значит новых внешних сигналов пробуждения не было.
                    // Оставляем также как сейчас, записывая данные в WakeUp component.

                    _processSetter.SetTimer(elem.Process, elem.Process.Process.TimerDate);
                    _processSetter.SetStatus(elem.Process, elem.Process.Process.Status);
                }
                else
                {
                    // Поступал новый внешний сигнал пробуждения, берем минимальную задержку таймера.
                    // Не засыпаем.
                    var nextTimerDate = DateTimeOffsetHelper.Min(elem.Process.Process.TimerDate, elem.WakeupWithLock.TimerDate);

                    _processSetter.SetTimer(elem.Process, nextTimerDate);
                    _processSetter.SetStatus(elem.Process, ProcessStatusEnum.AsyncExecute);
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
            var updateBuffer = new Dictionary<TId, (DateTimeOffset delayMinDate, ProcessWakeUpDbEntity<TId> Wakeup)>(data.Length);

            // Делаем попытки убедиться, что либо share lock если условие выполняется, либо updlock если не выполняется.
            
            for (int i = 0; i < _optionsDto.WakeupUpdLockRetryLimit; i++)
            {
                // 1) Если StreamActiveFlag, то обновлять ничего не нужно, достаточно ShareLock до конца транзакции.
                using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForShare))
                {
                    var wakeups = await _dbContext.Set<ProcessWakeUpDbEntity<TId>>()
                        .AsNoTracking()
                        .ApplayFilterCondition(
                            _processWakeUpDbEntityConditions.IsAsyncExecuting_TimerDate.QueryRange,
                            (
                                _dbContext,
                                checkBuffer.Select(e => (e.Key, e.Value)).ToArray()
                            )
                            )
                        .Select(e => e.ProcessId)
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
                    _optionsDto.WakeupEndUpdLockTimeout,
                    static async (p, t) =>
                    {
                        ProcessWakeUpDbEntity<TId>[] wakeupsWithLock;
                        using (var _ = p.This._lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                        {
                            wakeupsWithLock = await p.This._dbContext.Set<ProcessWakeUpDbEntity<TId>>()
                                .AsNoTracking()
                                .ApplayFilterCondition(p.This._processWakeUpDbEntityConditions.ProcessLinkedDbEntity.QueryRange, p.checkBuffer.Keys)
                                .ToArrayAsync(t);
                        }

                        // Блокировка получена.
                        foreach (var elem in wakeupsWithLock)
                        {
                            var checkDate = p.checkBuffer[elem.ProcessId];

                            if (p.This._processWakeUpDbEntityConditions.IsAsyncExecuting_TimerDate.Memory.Check(elem, checkDate))
                            {
                                // Кто-то уже обновил, Пробуждение не нужно.
                                p.checkBuffer.Remove(elem.ProcessId);
                            }
                            else
                            {
                                // Пробуждение нужно.
                                p.updateBuffer.Add(elem.ProcessId, (checkDate, elem));
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
                                _processDbEntityConditions.Id.QueryRange,
                                processIsActiveGroups.First(e => !e.Key).Select(e => e.ProcessId).ToArray())
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
                                _processDbEntityConditions.Id.QueryRange,
                                processIsActiveGroups.First(e => e.Key).Select(e => e.ProcessId).ToArray())
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
            var updateBuffer = new Dictionary<TId, ProcessWakeUpDbEntity<TId>>(data.Count);

            while (true) 
            {
                // 1) Если намерение выставлено - IsAsyncExecuting, то обновлять ничего не нужно, достаточно ShareLock до конца транзакции.
                using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForShare))
                {
                    var wakeups = await _dbContext.Set<ProcessWakeUpDbEntity<TId>>()
                        .AsNoTracking()
                        .ApplayFilterCondition(
                            _processWakeUpDbEntityConditions.ProcessLinkedDbEntity.QueryRange,
                            data
                            )
                        .ApplayFilterCondition(_processWakeUpDbEntityConditions.IsAsyncExecuting.Query, default)
                        .Select(e => e.ProcessId)
                        .ToArrayAsync(cancellationToken);

                    foreach (var elem in wakeups)
                    {
                        // Пробуждение не нужно.
                        checkBuffer.Remove(elem);
                    }
                }

                // 2) Пробуем получить updlock.
                var result = await TimeoutHelper.ExecuteWithTimeoutAsync(
                    (This: this, checkBuffer, updateBuffer),
                    _optionsDto.WakeupEndUpdLockTimeout,
                    static async (p,t) => 
                    {
                        using (var _ = p.This._lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                        {
                            var wakeupsWithLock = await p.This._dbContext.Set<ProcessWakeUpDbEntity<TId>>()
                                .AsNoTracking()
                                .ApplayFilterCondition(p.This._processWakeUpDbEntityConditions.ProcessLinkedDbEntity.QueryRange, p.checkBuffer)
                                .ToArrayAsync(t);

                            // У нас монопольная блокировка через updlock.
                            foreach (var elem in wakeupsWithLock)
                            {
                                if (p.This._processWakeUpDbEntityConditions.IsAsyncExecuting.Memory.Check(elem, default))
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
                        .ApplayFilterCondition(_processDbEntityConditions.Id.QueryRange, updateBuffer.Keys)
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
            public IProcessContainer<TId> Process { get; init; } = default!;

            public IWakeUpComponent WakeUpComponent { get; init; } = default!;

            /// <summary>
            /// Пробуждение с блокировкой.
            /// </summary>
            public ProcessWakeUpDbEntity<TId>? WakeupWithLock { get; set; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="WakeupEndUpdLockTimeout">Timeout попытки получения updlock на wakeup.</param>
        /// <param name="SessionEndUpdLockTimeout">В конце сессии timeout ожидания updlock на wakeup сущность.</param>
        /// <param name="WakeupUpdLockRetryLimit">Кол-во попыток получить блокировку для обновления даты wakeup.</param>
        /// <param name="ProcessCannotLockWakeupTimeout">Процесс хотел заснуть, но не смог получить updlock над wakeup сущности. Задержка перед следующей попыткой.</param>
        public record OptionsDto(
            TimeSpan WakeupEndUpdLockTimeout,
            TimeSpan SessionEndUpdLockTimeout,
            int WakeupUpdLockRetryLimit,
            TimeSpan ProcessCannotLockWakeupTimeout 
            )
        {
            public OptionsDto(
                TimeSpan? WakeupEndUpdLockTimeout = null,
                TimeSpan? SessionEndUpdLockTimeout = null,
                int? WakeupUpdLockRetryLimit = null,
                TimeSpan? ProcessCannotLockWakeupTimeout = null
                ) 
                : this(
                      WakeupEndUpdLockTimeout ?? TimeSpan.FromSeconds(2),
                      SessionEndUpdLockTimeout ?? TimeSpan.FromSeconds(2),
                      WakeupUpdLockRetryLimit ?? 2,
                      ProcessCannotLockWakeupTimeout ?? TimeSpan.FromSeconds(10)
                      )
            {
            }
        }
    }
}
