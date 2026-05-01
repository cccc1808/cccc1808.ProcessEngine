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
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;

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
        private readonly ITriggerEventRaiser _triggerEventRaiser;

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
            ITriggerEventRaiser triggerEventRaiser,

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
            _triggerEventRaiser = triggerEventRaiser;

            _processContainerConditions = processContainerConditions;
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
            static ExecuteContext BuildContext(
                EFWakeupService<TId> This,
                ICollection<IProcessContainer<TId>> processes)
            {
                var wakeupData = new Dictionary<TId, ExecuteWakeupContextItemDto>(processes.Count);
                var streamData = new Dictionary<TId, ExecuteStreamContextItemDto>(processes.Count);

                foreach (var elem in processes)
                {
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
                        // * Complete - необрабатывается
                        continue;
                    }

                    if (elem.TryGetComponent<IStreamTriggerComponent>(out var streamTriggerComponent))
                    {
                        // В текущей реализации stream trigger не используется проверку условия wakeup.
                        // Отчасти потому, что в таком случае есть шанс, что обработанное будет утеряно и не будет передано в StreamTrigger.
                        streamData.Add(
                            elem.Id,
                            new ExecuteStreamContextItemDto(elem, streamTriggerComponent));
                        continue;
                    }

                    // Нет компонента.
                    if (elem.TryGetComponent<IWakeupComponent>(out var wakeupComponent))
                    {
                        // Флаг - что мы вышли из части ассинхронного выполнения.
                        if (!wakeupComponent.InAsyncExecuting)
                        {
                            throw new InvalidOperationException("[Bug] Состояние.");
                        }

                        wakeupData.Add(
                            elem.Id,
                            new ExecuteWakeupContextItemDto(elem, wakeupComponent));
                    }
                }

                return new ExecuteContext(
                    wakeupData,
                    streamData
                    );
            }

            /// <summary>
            /// Блокировка используется, чтобы не допустить ситуации, когда другая транзакция попытается пробудить процесс,
            /// а мы это не увидим (и процесс уснет)
            /// (ждем завершения блокировок всех сигналов).
            /// </summary>
            static async Task LockWakeupStateAndCheckCondition(
                EFWakeupService<TId> This,
                IDictionary<TId, ExecuteWakeupContextItemDto> context, 
                CancellationToken cancellationToken) 
            {               
                // 1) Получаем wakeup с блокировкой.
                using (var hint = This._lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                {
                    var wakeUps = await This._dbContext.Set<ProcessWakeupDbEntity<TId>>()
                        .AsNoTracking()
                        .ApplayQueryCondition(This._processWakeUpDbEntityConditions.ProcessLinkedDbEntity.QueryRange, context.Keys)
                        .OrderBy(e => e.ProcessId) // Info: Для упорядоченной блокировки
                        .ToArrayAsync(cancellationToken);

                    foreach (var elem in wakeUps)
                    {
                        context[elem.ProcessId].UpdateLockHolded = true;
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
                    await elem.Key.HandleRangeAsync(
                        elem.Select(e => e.Element.Process).ToArray(),
                        cancellationToken);
                }
            }

            /// <summary>
            /// Обработка результатов, выставления статуса пробуждения.
            /// </summary>
            static void ExecuteWakeup(
                EFWakeupService<TId> This,
                IDictionary<TId, ExecuteWakeupContextItemDto> context) 
            {
                foreach (var elem in context.Values)
                {
                    if (elem.WakeUpComponent.HandlerResult)
                    {
                        var needUpdate = 
                            !elem.WakeUpComponent.IsAsyncExecuting 
                            || elem.Process.Process.Status != ProcessStatusEnum.AsyncExecute;

                        if (needUpdate)
                        {                            
                            elem.WakeUpComponent.NeedUpdate = true;
                            elem.WakeUpComponent.IsAsyncExecuting = true;
                            This._processSetter.SetStatus(elem.Process, ProcessStatusEnum.AsyncExecute);
                        }                        
                    }
                    else
                    {
                        var needUpdate =
                            elem.WakeUpComponent.IsAsyncExecuting
                            || elem.Process.Process.Status != ProcessStatusEnum.WaitEvent;

                        if (needUpdate)
                        {
                            elem.WakeUpComponent.NeedUpdate = true;
                            elem.WakeUpComponent.IsAsyncExecuting = false;
                            This._processSetter.SetStatus(elem.Process, ProcessStatusEnum.WaitEvent);
                        }
                    }
                } }

            static async ValueTask ExecuteStreamAsync(
                EFWakeupService<TId> This,
                IDictionary<TId, ExecuteStreamContextItemDto> streamData,
                CancellationToken cancellationToken) 
            {
                var processGoWaitTriggerEvents = streamData.Values
                    .Where(e => e.Process.Process.Status is ProcessStatusEnum.WaitEvent)
                    .Select(e => new ProcessGoWaitEvent(
                        e.StreamTriggerComponent.TriggerKey, 
                        e.StreamTriggerComponent.ProcessedChannels.ToDictionary()))
                    .ToArray();

                // Для процессов, использующих stream trigger, публикуем событие о том, что процесс засыпает и данные о смещения каналов.
                // Если поступают новые сигналы, то триггер пробудит процесс.
                await This._triggerEventRaiser.RaiseAsync(processGoWaitTriggerEvents, cancellationToken);
            }

            var context = BuildContext(this, processes);

            if (context.WakeupData.Any())
            {
                await LockWakeupStateAndCheckCondition(this, context.WakeupData, cancellationToken);
                ExecuteWakeup(this, context.WakeupData);

                await saveHandler(
                    context.WakeupData.Select(e => e.Value.Process).ToArray(),
                    cancellationToken);
            }

            if (context.StreamData.Any())
            {
                await ExecuteStreamAsync(this, context.StreamData, cancellationToken);
            }
        }

        public async Task WakeupProcessHandlerAsync(
            TId[] ids,
            CancellationToken cancellationToken)
        {
            if (ids.Length == 0)
            {
                return;
            }

            var checkBuffer = ids.ToHashSet();
            var updateBuffer = new Dictionary<TId, ProcessWakeupDbEntity<TId>>(ids.Length);

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
                    _optionsDto.WakeupEndUpdLockTimeout,
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
                foreach (var elem in updateBuffer.Values)
                {
                    elem.IsAsyncExecuting = true;
                }

                // Процессы не в состоянии обработки т.к. мы получили блокировку на wakeup, котрый в состоянии !IsAsyncExecuting.
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

                    elem.Status = ProcessStatusEnum.AsyncExecute;
                }
            }
        }

        #endregion

        private class ExecuteContext 
        {
            public IDictionary<TId, ExecuteWakeupContextItemDto> WakeupData { get; }

            public IDictionary<TId, ExecuteStreamContextItemDto> StreamData { get; }

            public ExecuteContext(
                IDictionary<TId, ExecuteWakeupContextItemDto> wakeupData,
                IDictionary<TId, ExecuteStreamContextItemDto> streamData)
            {
                WakeupData = wakeupData;
                StreamData = streamData;
            }
        }

        private class ExecuteWakeupContextItemDto
        {
            public IProcessContainer<TId> Process { get; }

            public IWakeupComponent WakeUpComponent { get; }

            public bool UpdateLockHolded { get; set; }

            public ExecuteWakeupContextItemDto(
                IProcessContainer<TId> process, 
                IWakeupComponent wakeUpComponent)
            {
                Process = process;
                WakeUpComponent = wakeUpComponent;

                UpdateLockHolded = false;
            }
        }

        private class ExecuteStreamContextItemDto 
        {
            public IProcessContainer<TId> Process { get; }

            public IStreamTriggerComponent StreamTriggerComponent { get; }

            public ExecuteStreamContextItemDto(
                IProcessContainer<TId> process, 
                IStreamTriggerComponent streamTriggerComponent)
            {
                Process = process;
                StreamTriggerComponent = streamTriggerComponent;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="WakeupEndUpdLockTimeout">Timeout попытки получения updlock на wakeup.</param>
        public record OptionsDto(
            TimeSpan WakeupEndUpdLockTimeout
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
