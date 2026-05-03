using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events.Stream;
using cccc1808.ProcessEngine.Model.Implementation.WakeupModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.WakeupModule.Services
{
    public class WakeupService<TId> 
        : IWakeupService<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IProcessSetter _processSetter;
        private readonly IWakeupRegistry<TId> _wakeupRegistry;
        private readonly IWakeupServiceQueries<TId> _wakeupServiceQueries;
        private readonly ITriggerEventRaiser _triggerEventRaiser;

        private readonly IProcessContainerConditions<TId> _processContainerConditions;

        private readonly OptionsDto _optionsDto;

        public WakeupService(
            IServiceProvider serviceProvider,
            IProcessSetter processSetter,
            IWakeupRegistry<TId> wakeupRegistry,
            IWakeupServiceQueries<TId> wakeupServiceQueries,
            ITriggerEventRaiser triggerEventRaiser,

            IProcessContainerConditions<TId> processContainerConditions,

            OptionsDto optionsDto)
        {
            _serviceProvider = serviceProvider;
            _processSetter = processSetter;
            _wakeupRegistry = wakeupRegistry;
            _wakeupServiceQueries = wakeupServiceQueries;
            _triggerEventRaiser = triggerEventRaiser;

            _processContainerConditions = processContainerConditions;

            _optionsDto = optionsDto;
        }

        #region IWakeUpService

        public async Task<ICollection<IProcessContainer<TId>>> AfterAsyncSessionHandlerAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            static ExecuteContext BuildContext(
                WakeupService<TId> This,
                ICollection<IProcessContainer<TId>> processes)
            {
                var wakeupWithoutStateData = new Dictionary<TId, ExecuteWakeupWithoutStateContextItemDto>(processes.Count);
                var wakeupWithStateData = new Dictionary<TId, ExecuteWakeupContextItemDto>(processes.Count);
                var streamData = new Dictionary<TId, ExecuteStreamContextItemDto>(processes.Count);

                foreach (var elem in processes)
                {
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

                    if (elem.TryGetComponent<ISimpleStreamTriggerComponent>(out var simpleStreamComponent))
                    {
                        // В текущей реализации stream trigger не используется проверку условия wakeup.
                        // Отчасти потому, что в таком случае есть шанс, что обработанное будет утеряно и не будет передано в StreamTrigger.
                        streamData.Add(
                            elem.Id,
                            new ExecuteStreamContextItemDto(elem, simpleStreamComponent, offsetStreamComponent: null));
                    }
                    else if (elem.TryGetComponent<IOffsetStreamTriggerComponent>(out var offsetStreamComponent))
                    {
                        // В текущей реализации stream trigger не используется проверку условия wakeup.
                        // Отчасти потому, что в таком случае есть шанс, что обработанное будет утеряно и не будет передано в StreamTrigger.
                        streamData.Add(
                            elem.Id,
                            new ExecuteStreamContextItemDto(elem, simpleStreamComponent: null, offsetStreamComponent));
                    }

                    switch (elem.WakeupState)
                    {
                        case WakeupStateEnum.WakeupWithState:
                            {
                                wakeupWithStateData.Add(
                                    elem.Id,
                                    new ExecuteWakeupContextItemDto(elem));
                                break;
                            }

                        case WakeupStateEnum.WakeupWithoutState:
                            {
                                wakeupWithoutStateData.Add(
                                    elem.Id,
                                    new ExecuteWakeupWithoutStateContextItemDto(elem));

                                break;
                            }

                        case WakeupStateEnum.NoWakeup: 
                            {
                                break;
                            }

                        default: throw new NotImplementedException("[Bug]");
                    }
                }

                return new ExecuteContext(
                    wakeupWithoutStateData,
                    wakeupWithStateData,
                    streamData);
            }

            static async Task ProcessWithoutStateAsync(
                WakeupService<TId> This,
                ExecuteContext context,
                CancellationToken cancellationToken) 
            {
                // Тут нет отдельного wakeup state, поэтому нужно только проверить условие.

                var checkGroups = context.WakeupWithoutStateData.Values
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
                        var needWakeup = result[elem2.Element.Process.Id];

                        if (needWakeup)
                        {
                            This._processSetter.SetStatus(
                                elem2.Element.Process,
                                ProcessStatusEnum.AsyncExecute);

                            elem2.Element.Process.AddComponent<IWakeupComponent<TId>>(
                                new WakeupComponent<TId>(
                                    id: default(TId),
                                    isAsyncExecuting: true,
                                    haveWakeupEntity: false,
                                    needUpdate: true)
                                );

                            context.ForUpdate.Add(elem2.Element.Process);
                        }
                    }
                }
            }

            /// <summary>
            /// Блокировка используется, чтобы не допустить ситуации, когда другая транзакция попытается пробудить процесс,
            /// а мы это не увидим (и процесс уснет)
            /// (ждем завершения блокировок всех сигналов).
            /// </summary>
            static async Task LockWakeupStateAndCheckCondition(
                WakeupService<TId> This,
                IDictionary<TId, ExecuteWakeupContextItemDto> context,
                CancellationToken cancellationToken)
            {
                // 1) Получаем wakeup с блокировкой
                // (это небходимо, чтобы если сейчас идет попытка пробуждения по новому внешнему сигналу, то она будет выполняться после завершения это транзакции, и новый сигнал не потеряется).
                var dbData = await This._wakeupServiceQueries.AfterSession_LoadStateWithLockAsync(
                    context.Keys,
                    cancellationToken);

                foreach (var elem in context.Values)
                {
                    var dbDataElem = dbData[elem.Process.Id];

                    elem.WakeupId = dbDataElem.Id;
                    elem.WakeupWithLockIsAsyncExecuting = dbDataElem.IsAsyncExecuting;
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
            static void ExecuteWakeup(
                WakeupService<TId> This,
                ExecuteContext context)
            {
                foreach (var elem in context.WakeupData.Values)
                {
                    var needUpdate = false;
                    var isAsyncExecuting = false;

                    if (elem.WakeupCheckResult)
                    {
                        needUpdate =
                            !elem.WakeupWithLockIsAsyncExecuting
                            || elem.Process.Process.Status != ProcessStatusEnum.AsyncExecute;
                        isAsyncExecuting = true;
                    }
                    else
                    {
                        needUpdate =
                            elem.WakeupWithLockIsAsyncExecuting
                            || elem.Process.Process.Status != ProcessStatusEnum.WaitEvent;
                        isAsyncExecuting = false;
                    }

                    if (needUpdate)
                    {
                        This._processSetter.SetStatus(
                            elem.Process,
                            isAsyncExecuting ? ProcessStatusEnum.AsyncExecute : ProcessStatusEnum.WaitEvent);

                        elem.Process.AddComponent<IWakeupComponent<TId>>(
                            new WakeupComponent<TId>(
                                elem.WakeupId,
                                isAsyncExecuting,
                                haveWakeupEntity: true,
                                needUpdate: true)
                            );

                        context.ForUpdate.Add(elem.Process);
                    }
                }
            }

            static async ValueTask ExecuteStreamAsync(
                WakeupService<TId> This,
                IDictionary<TId, ExecuteStreamContextItemDto> streamData,
                CancellationToken cancellationToken)
            {
                var processGoWaitTriggerEvents = streamData.Values
                    .Where(e => e.Process.Process.Status is ProcessStatusEnum.WaitEvent)
                    .Select(e => 
                        e.SimpleStreamComponent != null 
                            ? (ITriggerEvent)new ProcessGoWaitSpleepSimpleStreamEvent(e.SimpleStreamComponent.TriggerKey)
                            : (ITriggerEvent)new ProcessGoWaitSpleepOffsetStreamEvent(
                                e.OffsetStreamComponent.TriggerKey,
                                e.OffsetStreamComponent.ProcessedChannels.ToDictionary()
                                ))
                    .ToArray();

                // Для процессов, использующих stream trigger, публикуем событие о том, что процесс засыпает и данные о смещения каналов.
                // Если поступают новые сигналы, то триггер пробудит процесс.
                await This._triggerEventRaiser.RaiseAsync(processGoWaitTriggerEvents, cancellationToken);
            }

            var context = BuildContext(this, processes);

            // Сналача выполняет WakeupWithoutState т.к. им не нужна блокировка.
            if (context.WakeupWithoutStateData.Any())
            {
                await ProcessWithoutStateAsync(
                    this,
                    context,
                    cancellationToken);
            }

            List<IProcessContainer<TId>> forUpdate;
            if (context.WakeupData.Any())
            {
                await LockWakeupStateAndCheckCondition(this, context.WakeupData, cancellationToken);
                ExecuteWakeup(this, context);
            }
            else
            {
                forUpdate = new List<IProcessContainer<TId>>(0);
            }

            if (context.StreamData.Any())
            {
                await ExecuteStreamAsync(this, context.StreamData, cancellationToken);
            }

            return context.ForUpdate;
        }

        public async Task WakeupProcessHandlerAsync(
            ICollection<TId> ids,
            bool useShareLock,
            CancellationToken cancellationToken)
        {
            if (ids.Count == 0)
            {
                return;
            }

            var wakeupContext = await _wakeupServiceQueries.Wakeup_LoadStateAsync(
                ids, 
                useShareLock,
                _optionsDto.WakeupTryUpdatelockTimeout,
                cancellationToken);

            // Процессы не в состоянии обработки т.к. мы получили updatelock на wakeup и увидели статус WaitEvent.
            await _wakeupServiceQueries.Wakeup_LoadProcessesWithLockAsync(
                wakeupContext,
                cancellationToken);

            // 3) Обновляем wakeup и process
            foreach (var elem in wakeupContext.Data.Values)
            {
                var processState = elem.ProcessState 
                    ?? throw new InvalidOperationException($"[Bug]. Ожидается наличие {nameof(elem.ProcessState)}.");

                if (processState.StoppedByError || processState.RetryCount.HasValue) // TODO: condition
                {
                    // Если процесс в ошибке, то не трогаем его.
                    continue;
                }

                if (processState.Status == ProcessStatusEnum.Complete) // TODO: condition
                {
                    // Если процес завершился, то не трогам.
                    continue;
                }

                wakeupContext.ToWakeupData.Add(elem);
            }

            await _wakeupServiceQueries.Wakeup_ExecuteAsync(
                wakeupContext, 
                cancellationToken);
        }

        #endregion


        private class ExecuteContext
        {
            public IDictionary<TId, ExecuteWakeupWithoutStateContextItemDto> WakeupWithoutStateData { get; }

            public IDictionary<TId, ExecuteWakeupContextItemDto> WakeupData { get; }

            public IDictionary<TId, ExecuteStreamContextItemDto> StreamData { get; }

            public List<IProcessContainer<TId>> ForUpdate { get; }

            public ExecuteContext(
                IDictionary<TId, ExecuteWakeupWithoutStateContextItemDto> wakeupWithoutStateData,
                IDictionary<TId, ExecuteWakeupContextItemDto> wakeupData,
                IDictionary<TId, ExecuteStreamContextItemDto> streamData)
            {
                WakeupWithoutStateData = wakeupWithoutStateData;
                WakeupData = wakeupData;
                StreamData = streamData;
                ForUpdate = new List<IProcessContainer<TId>>(wakeupWithoutStateData.Count + wakeupData.Count);
            }
        }

        /// <summary>
        /// Пробуждение с проверкой условия без отдельной таблицы ProcessWakeup.
        /// </summary>
        private class ExecuteWakeupWithoutStateContextItemDto
        {
            public IProcessContainer<TId> Process { get; }

            public ExecuteWakeupWithoutStateContextItemDto(IProcessContainer<TId> process)
            {
                Process = process;
            }
        }

        /// <summary>
        /// Пробуждение с проверкой условия и отдельной таблицей ProcessWakeup.
        /// </summary>
        private class ExecuteWakeupContextItemDto
        {
            public IProcessContainer<TId> Process { get; }

            public TId WakeupId { get; set; }

            public bool WakeupWithLockIsAsyncExecuting { get; set; }

            public bool WakeupCheckResult { get; set; }

            public ExecuteWakeupContextItemDto(IProcessContainer<TId> process)
            {
                Process = process;

                WakeupId = default!;
                WakeupWithLockIsAsyncExecuting = false;
                WakeupCheckResult = false;
            }
        }

        /// <summary>
        /// Стрим по которому необходимо событие о засыпании.
        /// </summary>
        private class ExecuteStreamContextItemDto
        {
            public IProcessContainer<TId> Process { get; }

            public ISimpleStreamTriggerComponent SimpleStreamComponent { get; }

            public IOffsetStreamTriggerComponent OffsetStreamComponent { get; }

            public ExecuteStreamContextItemDto(
                IProcessContainer<TId> process,
                ISimpleStreamTriggerComponent simpleStreamComponent,
                IOffsetStreamTriggerComponent offsetStreamComponent)
            {
                Process = process;
                SimpleStreamComponent = simpleStreamComponent;
                OffsetStreamComponent = offsetStreamComponent;
            }
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
