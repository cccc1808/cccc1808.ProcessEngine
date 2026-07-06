using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Component.ActionComponent;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Handlers;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Service;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Implementation.Services
{
    public class TokenExecutionService<TId>
        : ITokenExecutionService<TId>
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IProcessRegistry _processRegistry;
        private readonly IProcessSetter _processSetter;
        private readonly ITriggerEventRaiser<TId> _triggerEventRaiser;
        private readonly IQueries _queries;
        private readonly ITriggerRepository<TId> _triggerRepository;
        private readonly ISchemaService<TId> _schemaService;

        private readonly OptionsDto _options;

        public TokenExecutionService(
            IDateTimeProvider dateTimeProvider,
            IProcessRegistry processRegistry,
            IProcessSetter processSetter,
            ITriggerEventRaiser<TId> triggerEventRaiser,
            IQueries queries,
            ITriggerRepository<TId> triggerRepository, 
            ISchemaService<TId> schemaService,

            OptionsDto options)
        {
            _dateTimeProvider = dateTimeProvider;
            _processRegistry = processRegistry;
            _processSetter = processSetter;
            _triggerEventRaiser = triggerEventRaiser;
            _queries = queries;
            _triggerRepository = triggerRepository;
            _schemaService = schemaService;

            _options = options;
        }

        #region ITokenExecutionService

        public async ValueTask ExecuteTokenAsync(
            IProcessContainer<TId> process,
            CancellationToken cancellationToken)
        {
            var processData = process.GetComponent<ISchemaProcessComponent>();
            var processHandler = _schemaService.GetProcessHandler(process.Process.Info.ProcessType);
            var processStateHandler = _schemaService.GetProcessStateHandler(process.Process.Info.ProcessType);
            var token = await _schemaService.GetSchemaToken(process.Process.Info.ProcessType, processData.CurrentTokenId, cancellationToken);

            // Снимок сигналов на момент начала обработки набора действий.
            var signalsSnapshot = process.Process.SignalCode;
            // Отбираем только те действия, которые активированы на текущий момент.
            var forExecuting = token.Actions
                .Where(
                    e => PrepareForExecuteAction(
                        process, 
                        processData,
                        signalsSnapshot,
                        e
                        )
                    )
                .ToArray();

            var actionsResult = ActionResult.EmptyResult();
            foreach (var elem in forExecuting)
            {
                var actionResult = await InnerExecuteActionAsync(
                    process,
                    processData,
                    processHandler,
                    token,
                    elem, 
                    cancellationToken);

                actionsResult = actionsResult.MergeFrom(actionResult);

                if (actionsResult.IsBreak)
                {
                    // Предпологается, что про произошло событие, которые завершает текущий токен:
                    // * Завершение процесса.
                    // * Переход на другой токен.

                    if (!actionsResult.IsAsyncExecuting)
                    {
                        // TODO: log warning.
                    }

                    break;
                }

                // Предпологается ошибка (если нужно, то статус переключен на WaitEvent).
                if (process.CurrentSession.CurrentSessionHaveError)
                {
                    // Предпологается триггер на Retry.
                    break;
                }

                // Ручная остановка асинхронной сессии.
                if (process.CurrentSession.StopAsyncProcessingSession)
                {
                    if (!actionsResult.IsAsyncExecuting)
                    { 
                        // TODO: log warning.
                    }

                    // Основной предпологаемый кейс - долгий ServiceTask, который еще не завершился, но произошел SoftTimeout.
                    break;
                }
            }

            await SetActionResultAsync(
                process,
                processData,
                token,
                actionsResult,
                cancellationToken);
        }

        public async ValueTask<bool> ExecuteActionAsync(
            IProcessContainer<TId> process,
            string actionId,
            BitFlagDto? signal,
            CancellationToken cancellationToken)
        {
            var processData = process.GetComponent<ISchemaProcessComponent>();
            var processHandler = _schemaService.GetProcessHandler(process.Process.Info.ProcessType);
            var token = await _schemaService.GetSchemaToken(process.Process.Info.ProcessType, processData.CurrentTokenId, cancellationToken);

            var isAsyncExecutingStatus = process.Process.Status is ProcessStatusEnum.AsyncExecute;
            var action = token.GetAction(actionId);

            if (isAsyncExecutingStatus)
            {
                // TODO: возможно log warning. Т.к. тут предпологается ProcessStatusEnum.WaitEvent.
            }

            // Если указан сигнал, то выставляем его.
            if (signal.HasValue)
            {
                _processSetter.SetSignalCode(
                    process,
                    process.Process.SignalCode.AddFlag(signal.Value), 
                    process.Process.SignalCodeFilter);                
            }

            var needExecute = PrepareForExecuteAction(
                process,
                processData,
                process.Process.SignalCode,
                action);
            if (!needExecute)
            {
                await SetActionResultAsync(
                    process,
                    processData,
                    token,
                    isAsyncExecutingStatus
                        ? ActionResult.AsyncExecutingResult() 
                        : ActionResult.EmptyResult(), 
                    cancellationToken);
                return false;
            }

            var actionResult = await InnerExecuteActionAsync(
                process,
                processData,
                processHandler,
                token,
                action,
                cancellationToken);

            if (
                isAsyncExecutingStatus 
                && !actionResult.IsAsyncExecuting
                && !actionResult.IsComplete)
            {
                // На начальный момент процес находился в статусе асинхронного выполнения.
                // Мы выполнили только одно действие их всех. На основании одного действия мы не можем переводить процесс в ProcessStatusEnum.WaitEvent.
                actionResult = actionResult.MergeFrom(
                    ActionResult.AsyncExecutingResult());
            }

            await SetActionResultAsync(
                process,
                processData,
                token,                
                actionResult,
                cancellationToken);

            return true;
        }

        public async ValueTask ValidateTokenState(
            IProcessContainer<TId> process, 
            string tokenId,
            string? conditionActionId,
            BitFlagDto? signalCode,
            CancellationToken cancellationToken)
        {
            static string BuildError(
                string tokenId,
                string? actionId,
                BitFlagDto? signalCodeFilter,
                string detail) => $"Ожидается активный токен и дейтсвие. {tokenId}. {actionId}. {signalCodeFilter?.Bits}. {detail}";

            var processData = process.GetComponent<ISchemaProcessComponent>();
            var processHandler = _schemaService.GetProcessHandler(process.Process.Info.ProcessType);
            var token = await _schemaService.GetSchemaToken(process.Process.Info.ProcessType, processData.CurrentTokenId, cancellationToken);

            if (signalCode.HasValue)
            {
                if (!_processRegistry.UseSignalCode(process.Process.Info.ProcessType))
                {
                    throw new Exception(
                        BuildError(tokenId, conditionActionId, signalCode, $"Процесс не использует коды сигналов."));
                }
            }

            if (tokenId != token.Id)
            {
                throw new Exception(
                    BuildError(tokenId, conditionActionId, signalCode, $"Фактический токен. {token.Id}"));
            }

            if (conditionActionId is not null)
            {
                var action = token.GetAction(conditionActionId);

                switch (action)
                {
                    case TimerTokenAction:
                    case ServiceTaskTokenAction:
                        throw new Exception(
                            BuildError(tokenId, conditionActionId, signalCode, $"Валидировать можно только {nameof(ConditionTokenAction)}. Указанно действие {action.GetType().Name}. {conditionActionId}"));

                    case ConditionTokenAction conditionTokenAction: 
                        {
                            if (!processData.TryGetActionState<ConditionActionStateComponent>(conditionActionId, out var state))
                            {
                                if (action.ActivatedOnStart)
                                {
                                    break;
                                }

                                throw new Exception(
                                    BuildError(tokenId, conditionActionId, signalCode, "Действие не запущено."));
                            }

                            switch (state.Status)
                            {
                                case ConditionActionStateComponent.StatusEnum.NoActivated:
                                    throw new Exception(
                                        BuildError(tokenId, conditionActionId, signalCode, "Действие не активировано."));

                                case ConditionActionStateComponent.StatusEnum.Complete:
                                    throw new Exception(
                                        BuildError(tokenId, conditionActionId, signalCode, "Действие завершено."));

                                case ConditionActionStateComponent.StatusEnum.WaitSignal:
                                case ConditionActionStateComponent.StatusEnum.CheckCondition:
                                    break;

                                default: 
                                    throw new NotImplementedException(state.Status.ToString());
                            }

                            if (signalCode.HasValue)
                            {
                                if (conditionTokenAction.Signal.Value != signalCode.Value.Bits)
                                {
                                    throw new Exception(
                                        BuildError(tokenId, conditionActionId, signalCode, "Указанное действие использует другой код сигнала."));
                                }

                                if (!process.Process.SignalCodeFilter.ContainsFlag(signalCode.Value))
                                {
                                    throw new Exception(
                                        BuildError(tokenId, conditionActionId, signalCode, "Процесс в текущий момент не одилает данный сигнал."));
                                }
                            }

                            break;
                        }

                    default: throw new NotImplementedException(action.GetType().FullName);
                }
            }
        }

        #endregion

        #region InnerExecuteActionAsync

        private async ValueTask<ActionResult> InnerExecuteActionAsync(
            IProcessContainer<TId> process,
            ISchemaProcessComponent component,
            ISchemaProcessHandler<TId> processHandler,
            TokenDto token,
            ITokenAction tokenAction,
            CancellationToken cancellationToken)
        {
            switch (tokenAction)
            {
                case TimerTokenAction timerTokenAction:
                    return await InnerExecuteActionAsync(
                        process,
                        component,
                        processHandler,
                        token,
                        timerTokenAction,
                        cancellationToken);

                case ConditionTokenAction conditionTokenAction:
                    return await InnerExecuteActionAsync(
                        process,
                        component,
                        processHandler,
                        token,
                        conditionTokenAction,
                        cancellationToken);

                case ServiceTaskTokenAction serviceTaskTokenAction:
                    return await InnerExecuteActionAsync(
                        process,
                        component,
                        processHandler,
                        token,
                        serviceTaskTokenAction,
                        cancellationToken);

                default:
                    throw new NotImplementedException(tokenAction.GetType().FullName);
            }
        }

        private async ValueTask<ActionResult> InnerExecuteActionAsync(
            IProcessContainer<TId> process,
            ISchemaProcessComponent component,
            ISchemaProcessHandler<TId> processHandler,
            TokenDto token,
            TimerTokenAction timerTokenAction,
            CancellationToken cancellationToken)
        {
            var state = GetOrCreateActionState(component, timerTokenAction, isActivate: false);

            switch (state.Status)
            {
                case TimerActionStateComponent.StatusEnum.NoActivated:
                case TimerActionStateComponent.StatusEnum.Complete:
                    return ActionResult.EmptyResult();

                case TimerActionStateComponent.StatusEnum.CreatingTimer:
                    {                        
                        state.Date = _dateTimeProvider.UtcNow + timerTokenAction.Duration;                      
                        if (timerTokenAction.Signal.HasValue)
                        {
                            state.Status = TimerActionStateComponent.StatusEnum.WaitSignal;
                        }
                        else 
                        {
                            state.Status = TimerActionStateComponent.StatusEnum.WaitingTimer;
                        }

                        state.TriggerKey = Guid.NewGuid().ToString();
                        await _triggerRepository.CreateTriggerAsync(
                            ITriggerRepository<TId>.CreateTriggerDto.TimerTrigger(
                                state.TriggerKey,
                                state.Date.Value,
                                process.Id,
                                isRangeTrigger: true,
                                handlerKey: _options.TimerTriggerHandler,
                                priority:
                                process.Process.Info.Priority,
                                isActivated: true,
                                isChildTrigger: true,
                                signal: timerTokenAction.Signal),
                            cancellationToken);

                        if (component.ProcessState is IProcessStateWithTriggers processStateWithTriggers)
                        {
                            processStateWithTriggers.TriggerState.Triggers.Add(
                                state.TriggerKey, 
                                new TriggerStateContainer.TriggerInfo(
                                    key: state.TriggerKey,
                                    removeTriggerQueueName: _options.AutoRemoveTriggerQueueName,
                                    removeTokenId: component.CurrentTokenId,
                                    removeIfTokenMove: true,
                                    removeIfProcessComplete: true));
                        }

                        return ActionResult.EmptyResult();
                    }

                case TimerActionStateComponent.StatusEnum.WaitingTimer:
                    {
                        _ = state.Date ?? throw new Exception("[Bug] Дата таймера не может быть пустой у взведенного таймера.");

                        // Условие выполнения действия.
                        var condition = _dateTimeProvider.UtcNow >= state.Date.Value;

                        if (!condition)
                        {
                            return ActionResult.EmptyResult();
                        }

                        state.Status = TimerActionStateComponent.StatusEnum.Complete;
                        if (component.ProcessState is IProcessStateWithTriggers processStateWithTriggers 
                            && state.TriggerKey is not null)
                        {
                            processStateWithTriggers.TriggerState.Triggers.Remove(state.TriggerKey);
                        }

                        var needAsyncExecuting = false;
                        if (timerTokenAction.HandlerKey is not null)
                        {
                            var executeResult = await processHandler.ExecuteTimerAsync(
                                new ISchemaProcessHandler<TId>.ExecuteParametersDto(
                                    timerTokenAction.HandlerKey,
                                    timerTokenAction.Id,
                                    process,
                                    component),
                                cancellationToken);

                            needAsyncExecuting = ActivateActions(
                                process,
                                token,
                                component,
                                executeResult.ActivateActions);
                        }

                        if (timerTokenAction.Transition.HasValue)
                        {
                            return TransitionResult(timerTokenAction.Transition.Value);
                        }

                        // Если была активация, то значит есть что выполнять.
                        if (needAsyncExecuting)
                        {
                            return ActionResult.AsyncExecutingResult();
                        }

                        return ActionResult.EmptyResult();
                    }

                default: 
                    throw new NotImplementedException(state.Status.ToString());
            }
        }

        private async ValueTask<ActionResult> InnerExecuteActionAsync(
            IProcessContainer<TId> process,
            ISchemaProcessComponent component,
            ISchemaProcessHandler<TId> processHandler,
            TokenDto token,
            ConditionTokenAction conditionTokenAction,
            CancellationToken cancellationToken)
        {
            var state = GetOrCreateActionState(component, conditionTokenAction, isActivate: false);

            switch (state.Status)
            {
                case ConditionActionStateComponent.StatusEnum.NoActivated:
                case ConditionActionStateComponent.StatusEnum.Complete:
                    return ActionResult.EmptyResult();

                case ConditionActionStateComponent.StatusEnum.CheckCondition:
                    {
                        var result = await processHandler.CheckConditionAsync(
                            new ISchemaProcessHandler<TId>.ExecuteParametersDto(
                                conditionTokenAction.CheckHandlerKey,
                                conditionTokenAction.Id,
                                process,
                                component),
                            cancellationToken);

                        if (!result)
                        {
                            return ActionResult.EmptyResult();
                        }

                        state.Status = ConditionActionStateComponent.StatusEnum.Complete;

                        var needAsyncExecuting = false;
                        if (conditionTokenAction.ActionHandlerKey is not null)
                        {
                            var executeResult = await processHandler.ExecuteConditionHandlerAsync(
                                new ISchemaProcessHandler<TId>.ExecuteParametersDto(
                                    conditionTokenAction.ActionHandlerKey,
                                    conditionTokenAction.Id,
                                    process,
                                    component),
                                cancellationToken);

                            needAsyncExecuting = ActivateActions(
                                process,
                                token,
                                component,
                                executeResult.ActivateActions);
                        }

                        if (conditionTokenAction.Transition.HasValue)
                        {
                            return TransitionResult(conditionTokenAction.Transition.Value);
                        }

                        // Если была активация, то значит есть что выполнять.
                        if (needAsyncExecuting)
                        {
                            return ActionResult.AsyncExecutingResult();
                        }

                        return ActionResult.EmptyResult();
                    }

                default: 
                    throw new NotImplementedException(state.Status.ToString());
            }
        }

        private async ValueTask<ActionResult> InnerExecuteActionAsync(
            IProcessContainer<TId> process,
            ISchemaProcessComponent component,
            ISchemaProcessHandler<TId> processHandler,
            TokenDto token,
            ServiceTaskTokenAction serviceTaskTokenAction,
            CancellationToken cancellationToken)
        {
            var state = GetOrCreateActionState(component, serviceTaskTokenAction, isActivate: false);

            switch (state.Status)
            {
                case ServiceTaskActionState.StatusEnum.NoActivated:
                case ServiceTaskActionState.StatusEnum.Complete:
                    return ActionResult.EmptyResult();

                case ServiceTaskActionState.StatusEnum.Executing:
                    {
                        var executeResult = await processHandler.ExecuteServiceTask(
                            new ISchemaProcessHandler<TId>.ExecuteParametersDto(
                                serviceTaskTokenAction.HandlerKey,
                                serviceTaskTokenAction.Id,
                                process,
                                component),
                            cancellationToken);

                        var needAsyncExecuting = ActivateActions(
                            process,
                            token,
                            component,
                            executeResult.ActivateActions);

                        if (!executeResult.IsComplete)
                        {
                            return ActionResult.AsyncExecutingResult();
                        }

                        state.Status = ServiceTaskActionState.StatusEnum.Complete;

                        if (serviceTaskTokenAction.Transition.HasValue)
                        {
                            return TransitionResult(serviceTaskTokenAction.Transition.Value);
                        }

                        // Если была активация, то значит есть что выполнять.
                        if (needAsyncExecuting)
                        {
                            return ActionResult.AsyncExecutingResult();
                        }

                        return ActionResult.EmptyResult();
                    }

                default: 
                    throw new NotImplementedException(state.Status.ToString());
            }
        }

        #endregion

        private async ValueTask SetActionResultAsync(
           IProcessContainer<TId> process,
           ISchemaProcessComponent component,
           TokenDto token,           
           ActionResult result,
           CancellationToken cancellationToken)
        {
            //static bool HaveAsyncExecutingActions(ISchemaProcessComponent component)
            //{
            //    return component.AllActionStates().Any(
            //        e => e switch
            //        {
            //            TimerActionStateComponent timerActionState => timerActionState.Status is TimerActionStateComponent.StatusEnum.CreatingTimer,
            //            ConditionActionStateComponent conditionActionState => false,
            //            ServiceTaskActionState serviceTaskActionState => serviceTaskActionState.Status is ServiceTaskActionState.StatusEnum.Executing,

            //            _ =>
            //            throw new NotImplementedException(e.GetType().FullName)
            //        }
            //        );
            //}

            static async ValueTask RemoveTriggersAsync(
                TokenExecutionService<TId> This,
                IProcessContainer<TId> process,
                ISchemaProcessComponent component,
                TokenDto token,
                bool isMoveOrComplete,
                CancellationToken cancellationToken)
            {
                if (component.ProcessState is IProcessStateWithTriggers processStateWithTriggers)
                {
                    var forRemove = isMoveOrComplete 
                        ? processStateWithTriggers.TriggerState.Triggers.Values
                            .Where(e =>
                                e.RemoveIfTokenMove
                                && (e.RemoveTokenId is null || e.RemoveTokenId == component.CurrentTokenId))
                            .ToArray()
                        : processStateWithTriggers.TriggerState.Triggers.Values
                            .Where(e => e.RemoveIfProcessComplete)
                            .ToArray();

                    if (!forRemove.Any())
                    {
                        return;
                    }

                    await This._triggerEventRaiser.RaiseAsync(
                        forRemove
                            .Select(e => new ITriggerEventRaiser<TId>.RaiseContainer(
                                e.RemoveTriggerQueueName,
                                process.Id,
                                new RemoveTriggerEvent(e.Key))
                            )
                            .ToArray(),
                        cancellationToken);

                    foreach (var elem in forRemove)
                    {
                        processStateWithTriggers.TriggerState.Triggers.Remove(elem.Key);
                    }
                }
            }

            static BitFlagDto GetSignalFilter(
                ISchemaProcessComponent component,
                TokenDto token) 
            {
                var result = BitFlagDto.Empty;

                foreach (var elem in component.AllActionStates())
                {
                    switch (elem)
                    {
                        case TimerActionStateComponent timerActionState:
                            {
                                if (timerActionState.Status is TimerActionStateComponent.StatusEnum.WaitingTimer)
                                {
                                    var action = (TimerTokenAction)token.GetAction(timerActionState.Id);
                                    if (action.Signal.HasValue && !timerActionState.IgnoreSignal)
                                    {
                                        result = result.AddFlag(action.Signal.Value);
                                    }
                                }

                                break;
                            }

                        case ConditionActionStateComponent conditionActionStateComponent:
                            {
                                if (conditionActionStateComponent.Status 
                                    is ConditionActionStateComponent.StatusEnum.WaitSignal
                                    or ConditionActionStateComponent.StatusEnum.CheckCondition)
                                {
                                    var action = (ConditionTokenAction)token.GetAction(conditionActionStateComponent.Id);
                                    if (action.Signal.HasValue && !conditionActionStateComponent.IgnoreSignal)
                                    {
                                        result = result.AddFlag(action.Signal.Value);
                                    }
                                }

                                break;
                            }
                    }
                }

                return result;
            }

            static async ValueTask UpdateSignalFilterAsync(
                TokenExecutionService<TId> This,
                IProcessContainer<TId> process,
                ISchemaProcessComponent component,
                TokenDto token,                
                BitFlagDto signal,
                BitFlagDto signalFilter,
                CancellationToken cancellationToken)
            {
                var haveChanges = This._processSetter.SetSignalCode(process, value: signal, signalFilter);
                if (!haveChanges)
                {
                    return;
                }

                await This._triggerEventRaiser.RaiseAsync(
                    [new ITriggerEventRaiser<TId>.RaiseContainer(
                        This._options.SignalFilterQueueName,
                        process.Id,
                        new SignalFilterRootTriggerEvent(
                            component.RootTriggerKey,
                            signalFilter.Bits)
                        )],
                    cancellationToken);
            }

            var useSignalCode = _processRegistry.UseSignalCode(process.Process.Info.ProcessType);

            // 1) Перемещение токена.
            if (result.MoveTokenId is not null)
            {
                // Автоматическое удаление триггеров при изменении токена.
                await RemoveTriggersAsync(
                    this,
                    process,
                    component,
                    token,
                    isMoveOrComplete: true,
                    cancellationToken);               
                
                // Меняем токен.
                component.MoveToken(
                    result.MoveTokenId);

                // Не обязательно тут (т.к. асинхронное выполнение продолжается), но пусть будет сброс.
                if (useSignalCode)
                {
                    // Обновляем фильтр сигнала.
                    // Сигнал сбрасываем, фильтр вычислиться автоматически при первом выполнении нового токена.
                    await UpdateSignalFilterAsync(
                        this,
                        process,
                        component,
                        token,
                        BitFlagDto.Empty,
                        BitFlagDto.Empty,
                        cancellationToken);
                }

                if (!process.InAsyncExecuting)
                {
                    // Продолжение асинхронного выполнения на другом токене.
                    _processSetter.SetStatus(process, ProcessStatusEnum.AsyncExecute);
                }

                return;
            }

            // 2) Завершение процесса.
            if (result.IsComplete)
            {
                // Автоматическое удаление триггеров при завершении процесса.
                await RemoveTriggersAsync(
                    this,
                    process,
                    component,
                    token,
                    isMoveOrComplete: false,
                    cancellationToken);

                // Завершение процесса.
                component.MoveToken(
                    component.CurrentTokenId);
                _processSetter.SetStatus(process, ProcessStatusEnum.Complete);
                _processSetter.SetSignalCode(process, value: BitFlagDto.Empty, filter: BitFlagDto.Empty);

                return;
            }

            // Вызов из асинхронного выполнения.
            if (process.InAsyncExecuting)
            {
                // Здесь работаем с намерением ProcessStatusEnum.AsyncExecute -> ProcessStatusEnum.WaitEvent

                if (result.IsAsyncExecuting)
                {
                    // Предпологается process.Process.Status == AsyncExecute
                    // Продолжение асинхронного выполнения на текущем токене.
                    return;
                }

                if (useSignalCode)
                {
                    // Обновляем фильтр сигнала.
                    await UpdateSignalFilterAsync(
                        this,
                        process,                        
                        component,
                        token,
                        process.Process.SignalCode,
                        GetSignalFilter(component, token),
                        cancellationToken);
                }

                if (process.CurrentSession.CurrentSessionHaveError)
                {
                    // Если ошибка в текущей сесси. То не трогаем статус.
                    return;
                }

                //var haveAsyncExecuting = HaveAsyncExecutingActions(component);

                //if (!haveAsyncExecuting)
                {
                    // Ожидание сигнала.
                    _processSetter.SetStatus(process, ProcessStatusEnum.WaitEvent);

                    // TODO:
                    if (_options.AutoDetectStreamTriggers)
                    {
                        var streamTriggerKeys = await _queries
                            .GetStreamTriggerKeysByProcessRangeAsync(process.Id, cancellationToken);                        

                        if (streamTriggerKeys.Any())
                        {
                            var streamTriggerComponent = new StreamTriggerComponent(_options.GoWaitTriggerQueueName, streamTriggerKeys);
                            process.AddComponent<IStreamTriggerComponent>(streamTriggerComponent);
                        }
                    }
                }
                //else
                {
                    // Продолжение асинхронного выполнения на текущем токене.
                }
            }
            // Вызов из внешнего кода.
            else
            {
                // Здесь работаем с намерением ProcessStatusEnum.WaitEvent -> ProcessStatusEnum.AsyncExecute

                // Предпологается process.Process.Status == ProcessStatusEnum.WaitEvent
                //var haveAsyncExecuting = HaveAsyncExecutingActions(component);

                if (result.IsAsyncExecuting /*|| haveAsyncExecuting*/)
                {
                    // Переход в асинхронное выполнение.
                    _processSetter.SetStatus(process, ProcessStatusEnum.AsyncExecute);
                }
                else
                {
                    // Продожаем ожидать следюущий сигнал.

                    // Обновляем фильтр сигнала.
                    await UpdateSignalFilterAsync(
                        this,
                        process, 
                        component, 
                        token, 
                        process.Process.SignalCode,
                        GetSignalFilter(component, token), 
                        cancellationToken);
                }
            }
        }

        /// <summary>
        /// Вызывается перед выполнением действия.
        /// * Проверяет поступление кода сигнала на действие.
        /// </summary>
        /// <returns>Нужна ли действию обработка асинзронного выполнения.</returns>
        private bool PrepareForExecuteAction(
            IProcessContainer<TId> process,
            ISchemaProcessComponent processData,
            BitFlagDto startSignals,
            ITokenAction tokenAction) 
        {
            switch (tokenAction)
            {
                case ServiceTaskTokenAction serviceTaskTokenAction:
                    {
                        var state = GetOrCreateActionState(processData, serviceTaskTokenAction, isActivate: false);

                        return state.Status
                            is ServiceTaskActionState.StatusEnum.Executing;
                    }

                case TimerTokenAction timerTokenAction:
                    {
                        var state = GetOrCreateActionState(processData, timerTokenAction, isActivate: false);

                        // Активируем проверку условия, если поступил сигнал с кодом.
                        if (timerTokenAction.Signal.HasValue && !state.IgnoreSignal)
                        {
                            if (state.Status is TimerActionStateComponent.StatusEnum.WaitSignal)
                            {
                                if (startSignals.ContainsFlag(timerTokenAction.Signal.Value))
                                {
                                    // Сигнал поступил на процесс.
                                    state.Status = TimerActionStateComponent.StatusEnum.WaitingTimer;
                                }
                            }

                            if (state.Status is TimerActionStateComponent.StatusEnum.WaitingTimer)
                            {
                                // Сигнал перевел condition в статус проверки, удаляем.
                                _processSetter.SetSignalCode(
                                    process,
                                    process.Process.SignalCode.RemoveFlag(timerTokenAction.Signal.Value),
                                    process.Process.SignalCodeFilter.RemoveFlag(timerTokenAction.Signal.Value));
                            }
                        }

                        return state.Status
                            is TimerActionStateComponent.StatusEnum.CreatingTimer
                            or TimerActionStateComponent.StatusEnum.WaitingTimer;
                    }

                case ConditionTokenAction conditionTokenAction:
                    {
                        var state = GetOrCreateActionState(processData, conditionTokenAction, isActivate: false);

                        // Активируем проверку условия, если поступил сигнал с кодом.
                        if (conditionTokenAction.Signal.HasValue && !state.IgnoreSignal)
                        {
                            if (state.Status is ConditionActionStateComponent.StatusEnum.WaitSignal)
                            {
                                if (startSignals.ContainsFlag(conditionTokenAction.Signal.Value))
                                {
                                    // Сигнал поступил на процесс.
                                    state.Status = ConditionActionStateComponent.StatusEnum.CheckCondition;
                                }
                            }

                            if (state.Status is ConditionActionStateComponent.StatusEnum.CheckCondition)
                            {
                                // Сигнал перевел condition в статус проверки, удаляем.
                                _processSetter.SetSignalCode(
                                    process,
                                    process.Process.SignalCode.RemoveFlag(conditionTokenAction.Signal.Value),
                                    process.Process.SignalCodeFilter.RemoveFlag(conditionTokenAction.Signal.Value));
                            }
                        }

                        return state.Status
                            is ConditionActionStateComponent.StatusEnum.CheckCondition;
                    }

                default:
                    throw new NotImplementedException(tokenAction.GetType().FullName);
            }
        }

        #region GetOrCreateActionState

        private static TimerActionStateComponent GetOrCreateActionState(
            ISchemaProcessComponent processData,
            TimerTokenAction tokenAction,
            bool isActivate)
        {
            // Существующий.
            if (processData.TryGetActionState<TimerActionStateComponent>(tokenAction.Id, out var state))
            {
                return state;
            }

            // Новый.
            var status = TimerActionStateComponent.StatusEnum.NoActivated;
            if (tokenAction.ActivatedOnStart || isActivate)
            {
                status = TimerActionStateComponent.StatusEnum.CreatingTimer;
            }

            state = new TimerActionStateComponent(
                tokenAction.Id,
                status);
            processData.AddActionState(state);

            return state;
        }

        private static ServiceTaskActionState GetOrCreateActionState(
            ISchemaProcessComponent processData,
            ServiceTaskTokenAction tokenAction,
            bool isActivate)
        {
            if (processData.TryGetActionState<ServiceTaskActionState>(tokenAction.Id, out var state))
            {
                return state;
            }

            state = new ServiceTaskActionState(
                tokenAction.Id,
                tokenAction.ActivatedOnStart || isActivate
                    ? ServiceTaskActionState.StatusEnum.Executing
                    : ServiceTaskActionState.StatusEnum.NoActivated);
            processData.AddActionState(state);

            return state;
        }

        private static ConditionActionStateComponent GetOrCreateActionState(
            ISchemaProcessComponent processData,
            ConditionTokenAction tokenAction,
            bool isActivate)
        {
            // Существующий.
            if (processData.TryGetActionState<ConditionActionStateComponent>(tokenAction.Id, out var state))
            {
                return state;
            }

            // Новый.
            var status = ConditionActionStateComponent.StatusEnum.NoActivated;
            if (tokenAction.ActivatedOnStart || isActivate)
            {
                if (!tokenAction.Signal.HasValue)
                {
                    status = ConditionActionStateComponent.StatusEnum.CheckCondition;
                }
                else 
                {
                    status = ConditionActionStateComponent.StatusEnum.WaitSignal;
                }                
            }

            state = new ConditionActionStateComponent(
                tokenAction.Id,
                status);
            processData.AddActionState(state);

            return state;
        }

        #endregion

        private static ActionResult TransitionResult(
            in ITokenAction.TransitionDto transition)
        {
            if (transition.IsComplete)
            {
                return ActionResult.CompleteResult();
            }
            else
            {
                return ActionResult.MoveResult(
                    transition.TargetTokenId ?? throw new Exception());
            }
        }

        private static bool ActivateActions(
            IProcessContainer<TId> process,
            TokenDto token,
            ISchemaProcessComponent component,
            ISchemaProcessHandler.ActivateActionDto[] activateActions)
        {
            var needAsyncExecute = false;

            foreach (var elem in activateActions)
            {
                //if (!tokenAction.ActivateRelations.ContainsKey(elem))
                //{
                //    throw new Exception(
                //        $"Действие пытается активировать действие, которое не задекларировано. {token.Id}. {elem}.");
                //}

                var activateAction = token.GetAction(elem.ActionId);
                switch (activateAction)
                {
                    case TimerTokenAction timerTokenAction:
                        {
                            var state = GetOrCreateActionState(component, timerTokenAction, isActivate: true);

                            switch (state.Status)
                            {
                                case TimerActionStateComponent.StatusEnum.NoActivated:
                                case TimerActionStateComponent.StatusEnum.Complete:
                                    {
                                        // Создаем таймер - асинхронное выполнение.
                                        state.Status = TimerActionStateComponent.StatusEnum.CreatingTimer;
                                        state.IgnoreSignal = false;
                                        needAsyncExecute = needAsyncExecute || true;

                                        break;
                                    }

                                case TimerActionStateComponent.StatusEnum.CreatingTimer:
                                    {
                                        // Создаем таймер - асинхронное выполнение.
                                        needAsyncExecute = needAsyncExecute || true;

                                        break;
                                    }

                                case TimerActionStateComponent.StatusEnum.WaitSignal:
                                    {
                                        // Уже активировано, ожидаем сигнала.
                                        needAsyncExecute = needAsyncExecute || false;

                                        break;
                                    }

                                case TimerActionStateComponent.StatusEnum.WaitingTimer:
                                    {
                                        // Если таймер уже создан, пусть ждет сигнал.
                                        needAsyncExecute = needAsyncExecute || false;

                                        break;
                                    }

                                default:
                                    throw new NotImplementedException(state.Status.ToString());
                            }


                            break;
                        }

                    case ConditionTokenAction conditionTokenAction:
                        {
                            var state = GetOrCreateActionState(component, conditionTokenAction, isActivate: true);

                            switch (state.Status)
                            {
                                case ConditionActionStateComponent.StatusEnum.NoActivated:
                                case ConditionActionStateComponent.StatusEnum.Complete:
                                    {
                                        if (conditionTokenAction.Signal.HasValue)
                                        {
                                            // Ожидание конкреного сигнала.
                                            var signalReceived = process.Process.SignalCode.ContainsFlag(conditionTokenAction.Signal.Value);

                                            if (!signalReceived)
                                            {
                                                state.Status = ConditionActionStateComponent.StatusEnum.WaitSignal;
                                                state.IgnoreSignal = false;
                                                needAsyncExecute = needAsyncExecute || elem.AsyncExecuteOrWaitSignal;
                                            }
                                            else
                                            {
                                                state.Status = ConditionActionStateComponent.StatusEnum.CheckCondition;
                                                state.IgnoreSignal = false;
                                                needAsyncExecute = needAsyncExecute || true;
                                            }
                                        }
                                        else
                                        {
                                            // Ожидание любого сигнала
                                            state.Status = ConditionActionStateComponent.StatusEnum.CheckCondition;
                                            needAsyncExecute = needAsyncExecute || elem.AsyncExecuteOrWaitSignal;
                                        }

                                        break;
                                    }

                                case ConditionActionStateComponent.StatusEnum.WaitSignal:
                                    {
                                        // Уже активировано, ожидаем сигнала.
                                        needAsyncExecute = needAsyncExecute || false;

                                        break;
                                    }

                                case ConditionActionStateComponent.StatusEnum.CheckCondition:
                                    {
                                        // Уже активировано, нужна ли проверка сейчас - как указал параметр.
                                        needAsyncExecute = needAsyncExecute || elem.AsyncExecuteOrWaitSignal;

                                        break;
                                    }

                                default:
                                    throw new NotImplementedException(state.Status.ToString());
                            }

                            break;
                        }

                    case ServiceTaskTokenAction serviceTaskTokenAction:
                        {
                            var state = GetOrCreateActionState(component, serviceTaskTokenAction, isActivate: true);

                            switch (state.Status)
                            {
                                case ServiceTaskActionState.StatusEnum.NoActivated:
                                case ServiceTaskActionState.StatusEnum.Complete:
                                    {
                                        // Действие запущено - выполняемся.
                                        state.Status = ServiceTaskActionState.StatusEnum.Executing;
                                        needAsyncExecute = needAsyncExecute || true;

                                        break;
                                    }

                                case ServiceTaskActionState.StatusEnum.Executing:
                                    {
                                        // Действие запущено - выполняемся.
                                        needAsyncExecute = needAsyncExecute || true;

                                        break;
                                    }

                                default:
                                    throw new NotImplementedException(state.Status.ToString());
                            }

                            break;
                        }
                }
            }

            return needAsyncExecute;
        }        

        #region types

        /// <summary>
        /// Результат обработки действия.
        /// </summary>
        /// <param name="IsBreak">Прервать выполнение дествие (не выполнять последующие).</param>
        /// <param name="IsComplete">Процесс должен завершится.</param>
        /// <param name="IsAsyncExecuting">Процесс должен находится в состоянии.</param>
        /// <param name="MoveTokenId">Необходимо перейти на указанный токен.</param>
        private readonly record struct ActionResult(
            bool IsBreak,
            bool IsComplete,
            bool IsAsyncExecuting,
            string? MoveTokenId)
        {
            public ActionResult MergeFrom(in ActionResult actionResult)
            {
                return this with 
                {
                    IsBreak = IsBreak || actionResult.IsBreak,
                    IsComplete = IsComplete || actionResult.IsComplete,
                    IsAsyncExecuting = IsAsyncExecuting || actionResult.IsAsyncExecuting,
                    MoveTokenId = actionResult.MoveTokenId ?? MoveTokenId
                };
            }

            public static ActionResult EmptyResult()
            {
                return new ActionResult(
                    IsBreak: false,
                    IsComplete: false,
                    IsAsyncExecuting: false,
                    MoveTokenId: null);
            }

            public static ActionResult CompleteResult()
            {
                return new ActionResult(
                    IsBreak: true,
                    IsComplete: true,
                    IsAsyncExecuting: false,
                    MoveTokenId: null);
            }

            public static ActionResult MoveResult(string moveTokenId)
            {
                return new ActionResult(
                    IsBreak: true, 
                    IsComplete: false, 
                    IsAsyncExecuting: true, 
                    MoveTokenId: moveTokenId);
            }

            public static ActionResult AsyncExecutingResult()
            {
                return new ActionResult(
                    IsBreak: false,
                    IsComplete: false,
                    IsAsyncExecuting: true,
                    MoveTokenId: null);
            }
        }

        public class OptionsDto 
        {
            public required string TimerTriggerHandler { get; set; }

            public required string GoWaitTriggerQueueName { get; set; }

            public required string SignalFilterQueueName { get; set; }

            public required string AutoRemoveTriggerQueueName { get; set; }

            public required bool AutoDetectStreamTriggers { get; set; }
        }

        public interface IQueries
        {
            Task<string[]> GetStreamTriggerKeysByProcessRangeAsync(
                TId processId, 
                CancellationToken cancellationToken);
        }

        #endregion
    }
}
