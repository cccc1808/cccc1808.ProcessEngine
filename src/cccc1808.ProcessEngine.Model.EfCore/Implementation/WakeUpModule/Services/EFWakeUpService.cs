using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeupModule.Services
{
    public class EFWakeupService<TId> 
        : IWakeupService<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEFDbContext _dbContext;
        private readonly IProcessSetter _processSetter;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IWakeupRegistry<TId> _wakeupRegistry;

        private readonly IProcessContainerConditions<TId> _processContainerConditions;
        private readonly IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> _processDbEntityConditions;
        private readonly IProcessWakeupDbEntityConditions<TId> _processWakeUpDbEntityConditions;        

        private readonly OptionsDto _optionsDto;

        public EFWakeupService(
            IServiceProvider serviceProvider,
            IEFDbContext dbContext,
            IProcessSetter processSetter,
            ILockQueryHintStore lockQueryHintStore,
            IWakeupRegistry<TId> wakeupRegistry,

            IProcessContainerConditions<TId> processContainerConditions,
            IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> processDbEntityConditions,
            IProcessWakeupDbEntityConditions<TId> processWakeUpDbEntityConditions,

            OptionsDto optionsDto)
        {
            _serviceProvider = serviceProvider;
            _dbContext = dbContext;
            _processSetter = processSetter;
            _lockQueryHintStore = lockQueryHintStore;
            _wakeupRegistry = wakeupRegistry;

            _processContainerConditions = processContainerConditions;
            _processDbEntityConditions = processDbEntityConditions;
            _processWakeUpDbEntityConditions = processWakeUpDbEntityConditions;

            _optionsDto = optionsDto;
        }

        #region IWakeUpService

        public async Task<ICollection<IProcessContainer<TId>>> AfterAsyncSessionHandlerAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            static Dictionary<TId, ExecuteContextItemDto> BuildContext(
                EFWakeupService<TId> This,
                ICollection<IProcessContainer<TId>> processes)
            {
                var context = new Dictionary<TId, ExecuteContextItemDto>(processes.Count);

                foreach (var elem in processes)
                {
                    if (!elem.UsingWakeup)
                    {
                        continue;
                    }

                    if (!elem.InAsyncExecuting)
                    {
                        throw new InvalidOperationException("[Bug] Данный метод вызывается только в конце асинхронной обработки.");
                    }

                    // Игнорируем процессы с ошибкой.
                    if (This._processContainerConditions.HaveError.Check(elem))
                    {
                        continue;
                    }

                    // Обрабатываем только указанные статусы.
                    if (elem.Process.Status is not ProcessStatusEnum.WaitEvent)
                    {
                        // Info:
                        // * AsyncExecuting - ничего обновлять и проверять не нужно (AsyncExecuting -> AsycnExecuting),
                        // * Complete - не обрабатывается
                        continue;
                    }
                    
                    context.Add(
                        elem.Id,
                        new ExecuteContextItemDto()
                        {
                            Process = elem,
                            WakeupWithLock = null
                        });
                }

                return context;
            }

            /// <summary>
            /// Блокировка используется, чтобы не допустить ситуации, когда другая транзакция попытается пробудить процесс,
            /// а мы это не увидим (и процесс уснет)
            /// (ждем завершения блокировок всех сигналов).
            /// </summary>
            static async Task LockWakeupStateAndCheckCondition(
                EFWakeupService<TId> This, 
                Dictionary<TId, ExecuteContextItemDto> context, 
                CancellationToken cancellationToken) 
            {               
                // 1) Получаем wakeup с блокировкой
                // (это небходимо, чтобы если сейчас идет попытка пробуждения по новому внешнему сигналу, то она будет выполняться после завершения это транзакции, и новый сигнал не потеряется).
                using (var hint = This._lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                {
                    var wakeUps = await This._dbContext.Set<ProcessWakeupDbEntity<TId>>()
                        .AsNoTracking()
                        .ApplayQueryCondition(This._processWakeUpDbEntityConditions.ProcessLinkedDbEntity.QueryRange, context.Keys)
                        .OrderBy(e => e.ProcessId) // Info: Для упорядоченной блокировки
                        .ToArrayAsync(cancellationToken);

                    foreach (var elem in wakeUps)
                    {
                        context[elem.ProcessId].WakeupWithLock = elem;
                    }
                }

                // 2) Блокировка получена, вызываем кастомную логику проверки условия: 
                var checkGroups = context.Values
                    .Select(e => (
                        Element: e,
                        Handler: This._wakeupRegistry.GetCheckHandler(This._serviceProvider, e.Process.Process.Info.ProcessType)))
                    .GroupBy(e => e.Handler);

                foreach (var elem in checkGroups)
                {
                    var processes = elem.Select(e => e.Element.Process).ToArray();

                    var result = await elem.Key.HandleRangeAsync(
                        processes,
                        cancellationToken);

                    foreach (var elem2 in elem)
                    {
                        elem2.Element.WakeupCheckResult = result[elem2.Element.Process.Id];
                    }
                }
            }

            /// <summary>
            /// Обработка результатов, выставления статуса пробуждения.
            /// </summary>
            static List<IProcessContainer<TId>> ExecuteWakeup(
                EFWakeupService<TId> This,
                Dictionary<TId, ExecuteContextItemDto> context) 
            {
                var forUpdate = new List<IProcessContainer<TId>>(context.Count);
                foreach (var elem in context.Values)
                {
                    var needUpdate = false;
                    var isAsyncExecuting = false;
                    if (elem.WakeupCheckResult)
                    {
                        needUpdate = 
                            !elem.WakeupWithLock.IsAsyncExecuting
                            || elem.Process.Process.Status != ProcessStatusEnum.AsyncExecute;
                        isAsyncExecuting = true;                       
                    }
                    else
                    {
                        needUpdate =
                            elem.WakeupWithLock.IsAsyncExecuting
                            || elem.Process.Process.Status != ProcessStatusEnum.WaitEvent;
                        isAsyncExecuting = false;
                    }

                    if (needUpdate)
                    {
                        This._processSetter.SetStatus(
                            elem.Process,
                            isAsyncExecuting ? ProcessStatusEnum.AsyncExecute : ProcessStatusEnum.WaitEvent);

                        elem.Process.AddComponent<IWakeupComponent>(
                            new EFWakeupProxyComponent<TId>(elem.WakeupWithLock)
                            {
                                IsAsyncExecuting = isAsyncExecuting,
                                NeedUpdate = true,
                            }
                            );
                        
                        This._dbContext.Set<ProcessWakeupDbEntity<TId>>().Attach(
                            new ProcessWakeupDbEntity<TId>(
                                id: elem.WakeupWithLock.Id,
                                processId: elem.Process.Id,
                                isAsyncExecuting: isAsyncExecuting
                                ))
                            .State = EntityState.Modified;

                        forUpdate.Add(elem.Process);
                    }
                }

                return forUpdate;
            }

            var context = BuildContext(this, processes);
            if (context.Count == 0)
            {
                return Array.Empty<IProcessContainer<TId>>();
            }

            await LockWakeupStateAndCheckCondition(this, context, cancellationToken);

            var forUpdate = ExecuteWakeup(this, context);
            return forUpdate;
        }

        public async Task WakeupProcessHandlerAsync(
            ICollection<TId> ids,
            CancellationToken cancellationToken)
        {
            if (ids.Count == 0)
            {
                return;
            }

            var checkBuffer = ids.ToHashSet();
            var updateBuffer = new Dictionary<TId, ProcessWakeupDbEntity<TId>>(ids.Count);

            while (true)
            {
                //// Замечание: share lock не является обязательно необходимым, может быть достаточной реализация только на основе update lock.

                // 1) Если намерение выставлено - IsAsyncExecuting, то обновлять ничего не нужно, достаточно ShareLock до конца транзакции.
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
                    _optionsDto.WakeupTryUpdatelockTimeout,
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

            // 3) Обновляем wakeup и process
            {
                // Процессы не в состоянии обработки т.к. мы получили updatelock на wakeup и увидели статус WaitEvent.
                ProcessDbEntity<TId>[] processes;
                using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                {
                    processes = await _dbContext.Set<ProcessDbEntity<TId>>()
                        .ApplayQueryCondition(_processDbEntityConditions.Id.QueryRange, updateBuffer.Keys)
                        .ToArrayAsync(cancellationToken);
                }

                foreach (var elem in processes)
                {
                    if (elem.StoppedByError || elem.RetryCount.HasValue) // TODO: condition
                    {
                        // Если процесс в ошибке, то не трогаем его.
                        continue;
                    }

                    if (elem.Status == ProcessStatusEnum.Complete) // TODO: condition
                    {
                        // Если процес завершился, то не трогам.
                        continue;
                    }

                    var wakeup = updateBuffer[elem.Id];

                    elem.Status = ProcessStatusEnum.AsyncExecute;
                    _dbContext.Set<ProcessWakeupDbEntity<TId>>().Attach(
                        new ProcessWakeupDbEntity<TId>(
                            id: wakeup.Id,
                            processId: elem.Id,
                            isAsyncExecuting: true
                            ))
                        .State = EntityState.Modified;
                }
            }
        }

        #endregion


        private class ExecuteContextItemDto
        {
            public IProcessContainer<TId> Process { get; init; } = default!;

            // public IWakeupComponent WakeUpComponent { get; init; } = default!;

            /// <summary>
            /// Пробуждение с блокировкой.
            /// </summary>
            public ProcessWakeupDbEntity<TId> WakeupWithLock { get; set; } = default!;

            public bool WakeupCheckResult { get; set; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="WakeupTryUpdatelockTimeout">Timeout попытки получения updlock на wakeup.</param>
        public record OptionsDto(
            TimeSpan WakeupTryUpdatelockTimeout
            )
        {
            public OptionsDto(
                TimeSpan? WakeupEndUpdLockTimeout = null
                ) 
                : this(
                      WakeupEndUpdLockTimeout ?? TimeSpan.FromSeconds(2)
                      )
            {
            }
        }
    }
}
