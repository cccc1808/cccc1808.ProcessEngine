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
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
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
        private readonly ITriggerEventRaiser<TId> _triggerEventRaiser;

        private readonly IProcessContainerConditions<TId> _processContainerConditions;

        private readonly OptionsDto _optionsDto;

        public WakeupService(
            IServiceProvider serviceProvider,
            IProcessSetter processSetter,
            IWakeupRegistry<TId> wakeupRegistry,
            IWakeupServiceQueries<TId> wakeupServiceQueries,
            ITriggerEventRaiser<TId> triggerEventRaiser,

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
                var streamData = new Dictionary<TId, StreamContextItemDto>(processes.Count);
                var offsetStreamData = new Dictionary<TId, OffsetContextItemDto>(processes.Count);
                
                foreach (var elem in processes)
                {
                    if (!elem.InAsyncExecuting)
                    {
                        throw new InvalidOperationException("[Bug] Данный метод вызывается только в конце асинхронной обработки.");
                    }

                    // Оповещение о смещении нужно отправить в любом случае.
                    if (elem.TryGetComponent<IOffsetTriggerComponent>(out var offsetStreamComponent)
                        && offsetStreamComponent.ProcessedOffsets.Any())
                    {
                        offsetStreamData.Add(
                            elem.Id,
                            new OffsetContextItemDto(elem, offsetStreamComponent));
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

                    // Потенциальное событие об остановке процесса.
                    if (elem.TryGetComponent<IStreamTriggerComponent>(out var streamTriggerComponent) 
                        && streamTriggerComponent.TriggersKeys.Any())
                    {
                        streamData.Add(
                            elem.Id,
                            new StreamContextItemDto(elem, streamTriggerComponent));
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
                    streamData,
                    offsetStreamData);
            }

            /// <summary>
            /// Обработка записей wakeup без отдельной таблицы.
            /// Подробности в <see cref="WakeupStateEnum.WakeupWithoutState"/>.
            /// </summary>
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
            /// Обработка записей wakeup с отдельной таблицей.
            /// Блокировка используется, чтобы не допустить ситуации, когда другая транзакция попытается пробудить процесс,
            /// а мы это не увидим (и процесс уснет)
            /// (ждем завершения блокировок всех сигналов).
            /// </summary>
            static async Task ProcessWithStateAsync(
                WakeupService<TId> This,
                ExecuteContext context,
                CancellationToken cancellationToken)
            {
                // 1) Получаем wakeup с блокировкой
                // (это небходимо, чтобы если сейчас идет попытка пробуждения по новому внешнему сигналу, то она будет выполняться после завершения это транзакции, и новый сигнал не потеряется).
                var dbData = await This._wakeupServiceQueries.AfterSession_LoadStateWithLockAsync(
                    context.WakeupData.Keys,
                    cancellationToken);

                foreach (var elem in context.WakeupData.Values)
                {
                    var dbDataElem = dbData[elem.Process.Id];

                    elem.WakeupId = dbDataElem.Id;
                    elem.WakeupWithLockIsAsyncExecuting = dbDataElem.IsAsyncExecuting;
                }
                
                // 2) Блокировка получена, вызываем кастомную логику проверки условия: 
                var checkGroups = context.WakeupData.Values
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
                        var needUpdate = false;
                        var isAsyncExecuting = false;

                        if (needWakeup)
                        {
                            needUpdate =
                                !elem2.Element.WakeupWithLockIsAsyncExecuting
                                || elem2.Element.Process.Process.Status != ProcessStatusEnum.AsyncExecute;
                            isAsyncExecuting = true;
                        }
                        else
                        {
                            needUpdate =
                                elem2.Element.WakeupWithLockIsAsyncExecuting
                                || elem2.Element.Process.Process.Status != ProcessStatusEnum.WaitEvent;
                            isAsyncExecuting = false;
                        }

                        if (needUpdate)
                        {
                            This._processSetter.SetStatus(
                                elem2.Element.Process,
                                isAsyncExecuting ? ProcessStatusEnum.AsyncExecute : ProcessStatusEnum.WaitEvent);

                            elem2.Element.Process.AddComponent<IWakeupComponent<TId>>(
                                new WakeupComponent<TId>(
                                    elem2.Element.WakeupId,
                                    isAsyncExecuting,
                                    haveWakeupEntity: true,
                                    needUpdate: true)
                                );

                            context.ForUpdate.Add(elem2.Element.Process);
                        }
                    }
                }
            }

            static async ValueTask ProcessOffsetTriggerAsync(
                WakeupService<TId> This,
                IDictionary<TId, OffsetContextItemDto> offsetData,
                CancellationToken cancellationToken)
            {
                // Замечание: Рассчет в том числе на то, что брокер гарантирует упорядоченность сообщений.
                // Инаеч лучше использовать общее событие на Offset и GoWait.
                var events = offsetData.Values
                    .SelectMany(
                        e => e.OffsetTriggerComponent
                            .ProcessedOffsets
                            .Select(e2 => (ProcessId: e.Process.Id, TriggerKey: e2.Key, ProcessedOffset: e2.Value))
                            )
                    .Select(e => new ProcessedOffsetTriggerEvent<TId>(e.ProcessId, e.TriggerKey, e.ProcessedOffset))
                    .ToArray();

                await This._triggerEventRaiser.RaiseAsync(events, cancellationToken);
            }
            
            static async ValueTask ProcessStreamTriggerAsync(
                WakeupService<TId> This,
                IDictionary<TId, StreamContextItemDto> streamData,
                CancellationToken cancellationToken)
            {
                // Оповещение указанных стрим триггеров о том, что процесс засыпает.
                var events = streamData.Values
                    .Where(e => e.Process.Process.Status == ProcessStatusEnum.WaitEvent)
                    .SelectMany(
                        e => e.StreamTriggerComponent
                            .TriggersKeys
                            .Select(e2 => (ProcessId: e.Process.Id, TriggerKey: e2))
                            )
                    .Select(e => new ProcessGoWaitStreamTriggerEvent<TId>(e.ProcessId, e.TriggerKey))
                    .ToArray();

                await This._triggerEventRaiser.RaiseAsync(events, cancellationToken);
            }

            var context = BuildContext(this, processes);

            // 1) Отправляем события по смещениям.
            if (context.OffsetData.Any())
            {
                await ProcessOffsetTriggerAsync(
                    this,
                    context.OffsetData,
                    cancellationToken);
            }

            // 2) Выполняет WakeupWithoutState т.к. им не нужна блокировка.
            if (context.WakeupWithoutStateData.Any())
            {
                await ProcessWithoutStateAsync(
                    this,
                    context,
                    cancellationToken);
            }

            // 3) Выполняем ProcessWithStateAsync, устанавливаем блокировку.
            if (context.WakeupData.Any())
            {
                await ProcessWithStateAsync
                    (this, 
                    context, 
                    cancellationToken);
            }

            // 4) Выполняем ProcessStreamTriggerAsync т.к. вышестоящий код мог менять статус процесса (он мог не уснуть).
            if (context.StreamData.Any())
            {
                await ProcessStreamTriggerAsync(
                    this, 
                    context.StreamData,
                    cancellationToken);
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

            public IDictionary<TId, StreamContextItemDto> StreamData { get; }

            public IDictionary<TId, OffsetContextItemDto> OffsetData { get; }

            public List<IProcessContainer<TId>> ForUpdate { get; }

            public ExecuteContext(
                IDictionary<TId, ExecuteWakeupWithoutStateContextItemDto> wakeupWithoutStateData,
                IDictionary<TId, ExecuteWakeupContextItemDto> wakeupData,
                IDictionary<TId, StreamContextItemDto> streamData,
                IDictionary<TId, OffsetContextItemDto> offsetData)
            {
                WakeupWithoutStateData = wakeupWithoutStateData;
                WakeupData = wakeupData;
                StreamData = streamData;
                OffsetData = offsetData;
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

            public ExecuteWakeupContextItemDto(IProcessContainer<TId> process)
            {
                Process = process;

                WakeupId = default!;
                WakeupWithLockIsAsyncExecuting = false;
            }
        }

        private class StreamContextItemDto
        {
            public IProcessContainer<TId> Process { get; }

            public IStreamTriggerComponent StreamTriggerComponent { get; }

            public StreamContextItemDto(
                IProcessContainer<TId> process,
                IStreamTriggerComponent streamTriggerComponent)
            {
                Process = process;
                StreamTriggerComponent = streamTriggerComponent;
            }
        }

        private class OffsetContextItemDto 
        {
            public IProcessContainer<TId> Process { get; }

            public IOffsetTriggerComponent OffsetTriggerComponent { get; }

            public OffsetContextItemDto(
                IProcessContainer<TId> process,
                IOffsetTriggerComponent offsetTriggerComponent)
            {
                Process = process;
                OffsetTriggerComponent = offsetTriggerComponent;
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
