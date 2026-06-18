using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component.ActionComponent;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Service;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Handlers;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Services
{
    public class EFTokenExecutionService<TId>
        : ITokenExecutionService<TId>
    {
        private readonly IEFDbContext _dbContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IProcessSetter _processSetter;
        private readonly ITriggerRepository<TId> _triggerRepository;
        private readonly ISchemaService<TId> _schemaService;

        public EFTokenExecutionService(
            IEFDbContext dbContext, 
            IDateTimeProvider dateTimeProvider,
            IProcessSetter processSetter,
            ITriggerRepository<TId> triggerRepository, 
            ISchemaService<TId> schemaService)
        {
            _dbContext = dbContext;
            _dateTimeProvider = dateTimeProvider;
            _processSetter = processSetter;
            _triggerRepository = triggerRepository;
            _schemaService = schemaService;
        }

        public async ValueTask ExecuteTokenAsync(
            IProcessContainer<TId> process,
            CancellationToken cancellationToken)
        {
            var processData = process.GetComponent<ISchemaProcessComponent>();
            var processHandler = _schemaService.GetProcessHandler(process.Process.Info.ProcessType);
            var processStateHandler = _schemaService.GetProcessStateHandler(process.Process.Info.ProcessType);
            var token = await _schemaService.GetSchemaToken(process.Process.Info.ProcessType, processData.CurrentTokenId, cancellationToken);

            var actionsResult = ActionResult.EmptyResult();

            foreach (var elem in token.Actions)
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
                    break;
                }

                // Предпологается ошибка (если нужно, то статус переключен на WaitEvent).
                if (process.CurrentSession.CurrentSessionHaveError)
                {
                    break;
                }

                // Ручная остановка асинхронной сессии.
                if (process.CurrentSession.StopAsyncProcessingSession)
                {
                    // Основной предпологаемый кейс - долгий ServiceTask, который еще не завершился, но произошел SoftTimeout.
                    break;
                }
            }

            await SetActionResultAsync(
                process,
                processData,
                actionsResult,
                cancellationToken);
        }

        public async ValueTask ExecuteActionAsync(
            IProcessContainer<TId> process, 
            string actionId, 
            CancellationToken cancellationToken,
            string? tokenId = null)
        {
            var processData = process.GetComponent<ISchemaProcessComponent>();
            var processHandler = _schemaService.GetProcessHandler(process.Process.Info.ProcessType);
            var token = await _schemaService.GetSchemaToken(process.Process.Info.ProcessType, processData.CurrentTokenId, cancellationToken);

            if (tokenId is not null && tokenId != token.Id)
            {
                throw new Exception(
                    $"Текущий токен процесса не соответсвует ожидаемому токену. {tokenId}, {token.Id}");
            }

            var actionResult = await InnerExecuteActionAsync(
                process,
                processData,
                processHandler,
                token,
                token.GetAction(actionId),
                cancellationToken
                );

            await SetActionResultAsync(
                process, 
                processData,
                actionResult,
                cancellationToken);
        }

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

        private async ValueTask<ActionResult> InnerExecuteActionAsync(IProcessContainer<TId> process,
            ISchemaProcessComponent component,
            ISchemaProcessHandler<TId> processHandler,
            TokenDto token,
            TimerTokenAction timerTokenAction,
            CancellationToken cancellationToken)
        {
            var state = GetOrCreateActionState(component, timerTokenAction);

            switch (state.Status)
            {
                case TimerActionStateComponent.StatusEnum.NoActivated:
                case TimerActionStateComponent.StatusEnum.Complete:
                    return ActionResult.EmptyResult();

                case TimerActionStateComponent.StatusEnum.CreatingTimer:
                    {
                        state.Status = TimerActionStateComponent.StatusEnum.WaitingTimer;
                        state.Date = _dateTimeProvider.UtcNow + timerTokenAction.Duration;                        

                        await _triggerRepository.CreateTriggerAsync(
                            ITriggerRepository<TId>.CreateTriggerDto.TimerTrigger(
                                Guid.NewGuid().ToString(),
                                state.Date.Value,
                                process.Id,
                                isRangeTrigger: true,
                                handlerKey: EFTimerChildTriggerHandler<TId>.Name,
                                priority:
                                process.Process.Info.Priority,
                                isActivated: true,
                                isChildTrigger: true),
                            cancellationToken);

                        return ActionResult.EmptyResult();
                    }

                case TimerActionStateComponent.StatusEnum.WaitingTimer:
                    {
                        // Условие выполнения действия.
                        var condition = _dateTimeProvider.UtcNow >= state.Date;

                        if (!condition)
                        {
                            return ActionResult.EmptyResult();
                        }

                        state.Status = TimerActionStateComponent.StatusEnum.Complete;

                        var haveActivations = false;
                        if (timerTokenAction.HandlerKey is not null)
                        {
                            var executeResult = await processHandler.ExecuteTimerAsync(
                                new ISchemaProcessHandler<TId>.ExecuteParametersDto(
                                    timerTokenAction.HandlerKey,
                                    timerTokenAction.Id,
                                    process,
                                    component),
                                cancellationToken);

                            haveActivations = ActivateActions(
                                token,
                                component,
                                executeResult.ActivateActions);
                        }

                        if (timerTokenAction.Transition.HasValue)
                        {
                            return TransitionResult(timerTokenAction.Transition.Value);
                        }

                        // Если была активация, то значит есть что выполнять.
                        if (haveActivations)
                        {
                            return ActionResult.AsyncExecutingResult();
                        }

                        return ActionResult.EmptyResult();
                    }

                default: 
                    throw new NotImplementedException(state.Status.ToString());
            }
        }

        private async ValueTask<ActionResult> InnerExecuteActionAsync(IProcessContainer<TId> process,
            ISchemaProcessComponent component,
            ISchemaProcessHandler<TId> processHandler,
            TokenDto token,
            ConditionTokenAction conditionTokenAction,
            CancellationToken cancellationToken)
        {
            var state = GetOrCreateActionState(component, conditionTokenAction);

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

                        var haveActivations = false;
                        if (conditionTokenAction.ActionHandlerKey is not null)
                        {
                            var executeResult = await processHandler.ExecuteConditionHandlerAsync(
                                new ISchemaProcessHandler<TId>.ExecuteParametersDto(
                                    conditionTokenAction.ActionHandlerKey,
                                    conditionTokenAction.Id,
                                    process,
                                    component),
                                cancellationToken);

                            haveActivations = ActivateActions(
                                token,
                                component,
                                executeResult.ActivateActions);
                        }

                        if (conditionTokenAction.Transition.HasValue)
                        {
                            return TransitionResult(conditionTokenAction.Transition.Value);
                        }

                        // Если была активация, то значит есть что выполнять.
                        if (haveActivations)
                        {
                            return ActionResult.AsyncExecutingResult();
                        }

                        return ActionResult.EmptyResult();
                    }

                default: 
                    throw new NotImplementedException(state.Status.ToString());
            }
        }

        private async ValueTask<ActionResult> InnerExecuteActionAsync(IProcessContainer<TId> process,
            ISchemaProcessComponent component,
            ISchemaProcessHandler<TId> processHandler,
            TokenDto token,
            ServiceTaskTokenAction serviceTaskTokenAction,
            CancellationToken cancellationToken)
        {
            var state = GetOrCreateActionState(component, serviceTaskTokenAction);

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

                        var haveActivations = ActivateActions(
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
                        if (haveActivations)
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
           ActionResult result,
           CancellationToken cancellationToken)
        {
            static bool HaveAsyncExecutingActions(ISchemaProcessComponent component)
            {
                return component.AllActionStates().Any(
                    e => e switch 
                    {
                        TimerActionStateComponent timerActionState => timerActionState.Status is TimerActionStateComponent.StatusEnum.CreatingTimer,
                        ConditionActionStateComponent conditionActionState => false,
                        ServiceTaskActionState serviceTaskActionState => serviceTaskActionState.Status is ServiceTaskActionState.StatusEnum.Executing,

                        _ => 
                        throw new NotImplementedException(e.GetType().FullName)
                    }
                    );
            }

            if (result.MoveTokenId is not null)
            {
                // Меняем токен.
                component.MoveToken(
                    result.MoveTokenId);

                if (!process.InAsyncExecuting)
                {
                    // Продолжение асинхронного выполнения на другом токене.
                    _processSetter.SetStatus(process, ProcessStatusEnum.AsyncExecute);
                }

                return;
            }

            if (result.IsComplete)
            {
                // Завершение процесса.
                component.MoveToken(
                    component.CurrentTokenId);
                _processSetter.SetStatus(process, ProcessStatusEnum.Complete);

                return;
            }

            // Вызов из асинхронного выполнения.
            if (process.InAsyncExecuting)
            {
                if (result.IsAsyncExecuting)
                {
                    // Предпологается process.Process.Status == ProcessStatusEnum.AsyncExecute
                    // Продолжение асинхронного выполнения на текущем токене.
                    return;
                }

                if (process.CurrentSession.CurrentSessionHaveError)
                {
                    // Если ошибка в текущей сесси. То не трогаем статус.
                    return;
                }

                var haveAsyncExecuting = HaveAsyncExecutingActions(component);

                if (!haveAsyncExecuting)
                {
                    // Ожидание сигнала.
                    _processSetter.SetStatus(process, ProcessStatusEnum.WaitEvent);

                    // TODO:
                    if (component.AutoDetectStreamTriggers)
                    {
                        ITriggerComponent.TriggerKind[] streamKinds = [ITriggerComponent.TriggerKind.SimpleStream, ITriggerComponent.TriggerKind.SimpleStreamRoot, ITriggerComponent.TriggerKind.OffsetStream];
                        var streamTriggerKeys = await _dbContext.Set<TriggerDbEntity<TId>>()
                            .Where(
                                e => e.ProcessId.Equals(process.Id)
                                    && streamKinds.Contains(e.Kind)
                                    && !e.IsCompleted)
                            .Select(e => e.Key)
                            .ToArrayAsync(cancellationToken);

                        if (streamTriggerKeys.Any())
                        {
                            var streamTriggerComponent = new StreamTriggerComponent("trigger_events", streamTriggerKeys);
                            process.AddComponent<IStreamTriggerComponent>(streamTriggerComponent);
                        }
                    }
                }
                else
                {
                    // Продолжение асинхронного выполнения на текущем токене.
                }
            }
            // Вызов из внешнего кода.
            else
            {
                // Предпологается process.Process.Status == ProcessStatusEnum.WaitEvent
                var haveAsyncExecuting = HaveAsyncExecutingActions(component);

                if (result.IsAsyncExecuting || haveAsyncExecuting)
                {
                    // Переход в асинхронное выполнение.
                    _processSetter.SetStatus(process, ProcessStatusEnum.AsyncExecute);
                }
                else
                {
                    // Продожаем ожидать следюущий сигнал.
                }
            }
        }

        private static TimerActionStateComponent GetOrCreateActionState(
            ISchemaProcessComponent processData,
            TimerTokenAction tokenAction)
        {
            if (processData.TryGetActionState<TimerActionStateComponent>(tokenAction.Id, out var state))
            {
                return state;
            }

            state = new TimerActionStateComponent(
                tokenAction.Id,
                tokenAction.ActivatedOnStart
                ? TimerActionStateComponent.StatusEnum.CreatingTimer
                : TimerActionStateComponent.StatusEnum.NoActivated);
            processData.AddActionState(state);

            return state;
        }

        private static ServiceTaskActionState GetOrCreateActionState(
            ISchemaProcessComponent processData,
            ServiceTaskTokenAction tokenAction)
        {
            if (processData.TryGetActionState<ServiceTaskActionState>(tokenAction.Id, out var state))
            {
                return state;
            }

            state = new ServiceTaskActionState(
                tokenAction.Id,
                tokenAction.ActivatedOnStart
                ? ServiceTaskActionState.StatusEnum.Executing
                : ServiceTaskActionState.StatusEnum.NoActivated);
            processData.AddActionState(state);

            return state;
        }

        private static ConditionActionStateComponent GetOrCreateActionState(
            ISchemaProcessComponent processData,
            ConditionTokenAction tokenAction)
        {
            if (processData.TryGetActionState<ConditionActionStateComponent>(tokenAction.Id, out var state))
            {
                return state;
            }

            state = new ConditionActionStateComponent(
                tokenAction.Id,
                tokenAction.ActivatedOnStart
                ? ConditionActionStateComponent.StatusEnum.CheckCondition
                : ConditionActionStateComponent.StatusEnum.NoActivated);
            processData.AddActionState(state);

            return state;
        }

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
            TokenDto token,
            ISchemaProcessComponent component,
            string[] activateActions)
        {
            var haveActivations = false;

            foreach (var elem in activateActions)
            {
                //if (!tokenAction.ActivateRelations.ContainsKey(elem))
                //{
                //    throw new Exception(
                //        $"Действие пытается активировать действие, которое не задекларировано. {token.Id}. {elem}.");
                //}

                var activateAction = token.GetAction(elem);
                switch (activateAction)
                {
                    case TimerTokenAction timerTokenAction:
                        {
                            var state = GetOrCreateActionState(component, timerTokenAction);

                            switch (state.Status)
                            {
                                case TimerActionStateComponent.StatusEnum.NoActivated:
                                case TimerActionStateComponent.StatusEnum.Complete:
                                    {
                                        state.Status = TimerActionStateComponent.StatusEnum.CreatingTimer;
                                        haveActivations = true;
                                        break;
                                    }

                                case TimerActionStateComponent.StatusEnum.CreatingTimer:
                                case TimerActionStateComponent.StatusEnum.WaitingTimer:
                                    {
                                        break;
                                    }

                                default:
                                    throw new NotImplementedException(state.Status.ToString());
                            }


                            break;
                        }

                    case ConditionTokenAction conditionTokenAction:
                        {
                            var state = GetOrCreateActionState(component, conditionTokenAction);

                            switch (state.Status)
                            {
                                case ConditionActionStateComponent.StatusEnum.NoActivated:
                                case ConditionActionStateComponent.StatusEnum.Complete:
                                    {
                                        state.Status = ConditionActionStateComponent.StatusEnum.CheckCondition;
                                        haveActivations = true;
                                        break;
                                    }

                                case ConditionActionStateComponent.StatusEnum.CheckCondition:
                                    {
                                        break;
                                    }

                                default:
                                    throw new NotImplementedException(state.Status.ToString());
                            }

                            break;
                        }

                    case ServiceTaskTokenAction serviceTaskTokenAction:
                        {
                            var state = GetOrCreateActionState(component, serviceTaskTokenAction);

                            switch (state.Status)
                            {
                                case ServiceTaskActionState.StatusEnum.NoActivated:
                                case ServiceTaskActionState.StatusEnum.Complete:
                                    {
                                        state.Status = ServiceTaskActionState.StatusEnum.Executing;
                                        haveActivations = true;

                                        break;
                                    }

                                case ServiceTaskActionState.StatusEnum.Executing:
                                    {
                                        break;
                                    }

                                default:
                                    throw new NotImplementedException(state.Status.ToString());
                            }

                            break;
                        }
                }
            }

            return haveActivations;
        }

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
    }
}
