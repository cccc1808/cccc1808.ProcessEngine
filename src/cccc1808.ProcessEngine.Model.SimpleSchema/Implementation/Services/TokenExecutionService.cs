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
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Component.ActionComponent;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Handlers;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Service;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Service;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Implementation.Services
{
    public class TokenExecutionService<TId>
        : ITokenExecutionService<TId>
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ISchemaProcessActionSetter _schemaProcessActionSetter;
        private readonly IProcessSetter _processSetter;
        private readonly IQueries _queries;
        private readonly ITriggerRepository<TId> _triggerRepository;
        private readonly ISchemaService<TId> _schemaService;
        private readonly ITriggerStateService<TId> _triggerStateService;

        private readonly OptionsDto _options;

        public TokenExecutionService(
            IDateTimeProvider dateTimeProvider,
            ISchemaProcessActionSetter schemaProcessActionSetter,
            IProcessSetter processSetter,
            IQueries queries,
            ITriggerRepository<TId> triggerRepository,
            ISchemaService<TId> schemaService,
            ITriggerStateService<TId> triggerStateService,

            OptionsDto options)
        {
            _dateTimeProvider = dateTimeProvider;
            _schemaProcessActionSetter = schemaProcessActionSetter;
            _processSetter = processSetter;
            _queries = queries;
            _triggerRepository = triggerRepository;
            _schemaService = schemaService;
            _triggerStateService = triggerStateService;

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

            // Отбираем только те действия, которые активированы на текущий момент.
            var forExecuting = token.Actions
                .Where(
                    e => PrepareForExecuteAction(
                        process,
                        processData,
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

        public async ValueTask ExecuteActionAsync(
            IProcessContainer<TId> process,
            string actionId,
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

            var needExecute = PrepareForExecuteAction(
                process,
                processData,
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
                // Мы выполнили только одно действие из всех. На основании одного действия мы не можем переводить процесс в ProcessStatusEnum.WaitEvent.
                actionResult = actionResult.MergeFrom(
                    ActionResult.AsyncExecutingResult());
            }

            await SetActionResultAsync(
                process,
                processData,
                token,
                actionResult,
                cancellationToken);
        }

        public async ValueTask ValidateTokenState(
            IProcessContainer<TId> process,
            string tokenId,
            string? conditionActionId,
            CancellationToken cancellationToken)
        {
            static string BuildError(
                string tokenId,
                string? actionId,
                string detail) => $"Ожидается активный токен и действие. {tokenId}. {actionId}. {detail}";

            var processData = process.GetComponent<ISchemaProcessComponent>();
            var processHandler = _schemaService.GetProcessHandler(process.Process.Info.ProcessType);
            var token = await _schemaService.GetSchemaToken(process.Process.Info.ProcessType, processData.CurrentTokenId, cancellationToken);

            if (tokenId != token.Id)
            {
                throw new Exception(
                    BuildError(tokenId, conditionActionId, $"Фактический токен. {token.Id}"));
            }

            if (conditionActionId is not null)
            {
                var action = token.GetAction(conditionActionId);

                _schemaProcessActionSetter.CommonSetter.OneOfWithState(
                    (This: this, BuildError: (Func<string, string, string, string>)BuildError, tokenId, conditionActionId, action),
                    processData,
                    action,
                    activateIfCreate: false,
                    serviceTaskHandler: static (p, action, state) =>
                        throw new Exception(
                            p.BuildError(
                                p.tokenId,
                                p.conditionActionId,
                                $"Валидировать можно только {nameof(ConditionTokenAction)}. Указано действие {action.GetType().Name}. {p.conditionActionId}"
                                )
                            ),
                    serviceTaskNotExsistStateHandler: static (p, action) =>
                        throw new Exception(
                            p.BuildError(
                                p.tokenId,
                                p.conditionActionId,
                                $"Валидировать можно только {nameof(ConditionTokenAction)}. Указано действие {action.GetType().Name}. {p.conditionActionId}"
                                )
                            ),
                    conditionHandler: static (p, action, state) => 
                    {
                        p.This._schemaProcessActionSetter.ConditionSetter.OneOfStatus(
                            p,
                            state.Status,
                            noActivatedHandler: static (p) => 
                                throw new Exception(
                                    p.BuildError(p.tokenId, p.conditionActionId, "Действие не активировано.")),
                            checkConditionHandler: static (p) => 1,
                            completeHandler: static (p) =>
                                throw new Exception(
                                    BuildError(p.tokenId, p.conditionActionId, "Действие завершено."))
                                );

                        return 1;
                    },
                    conditionNotExsistStateHandler: static (p, action) => 
                    {
                        if (!action.ActivatedOnStart)
                        {
                            throw new Exception(
                                p.BuildError(p.tokenId, p.conditionActionId, "Действие не запущено."));
                        }
                    },
                    timerHandler: static (p, action, state) => 
                    {
                        throw new Exception(
                            p.BuildError(
                                p.tokenId,
                                p.conditionActionId,
                                $"Валидировать можно только {nameof(ConditionTokenAction)}. Указано действие {action.GetType().Name}. {p.conditionActionId}")
                            );
                    },
                    timerTaskNotExsistStateHandler: static  (p, action) =>
                        throw new Exception(
                            p.BuildError(
                                p.tokenId,
                                p.conditionActionId,
                                $"Валидировать можно только {nameof(ConditionTokenAction)}. Указано действие {action.GetType().Name}. {p.conditionActionId}")
                            )
                    );
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
            return await _schemaProcessActionSetter.CommonSetter.OneOfWithStateAsync(
                (This: this, processHandler, process, component, token),
                component,
                tokenAction,
                activateIfCreate: false,
                serviceTaskHandler: static async (p, action, actionState, t) => await p.This.InnerExecuteActionAsync(
                    p.process, 
                    p.component,
                    p.processHandler,
                    p.token,
                    action,
                    actionState,
                    t),
                conditionHandler: static async (p, action, actionState, t) => await p.This.InnerExecuteActionAsync(
                    p.process,
                    p.component,
                    p.processHandler,
                    p.token,
                    action,
                    actionState,
                    t),
                timerHandler: static async (p, action, actionState, t) => await p.This.InnerExecuteActionAsync(
                    p.process,
                    p.component,
                    p.processHandler,
                    p.token,
                    action,
                    actionState,
                    t),
                cancellationToken);
        }

        private async ValueTask<ActionResult> InnerExecuteActionAsync(
            IProcessContainer<TId> process,
            ISchemaProcessComponent component,
            ISchemaProcessHandler<TId> processHandler,
            TokenDto token,
            ServiceTaskTokenAction serviceTaskTokenAction,
            ServiceTaskActionState state,
            CancellationToken cancellationToken)
        {
            return await _schemaProcessActionSetter.ServiceTaskSetter.OneOfStatusAsync(
                (This: this, processHandler, process, component, token, serviceTaskTokenAction, state),
                state.Status,
                noActivatedHandler: static (p, t) => ValueTask.FromResult(ActionResult.EmptyResult()),
                executingHandler: static async (p, t) => 
                {
                    var executeResult = await p.processHandler.ExecuteServiceTask(
                        new ISchemaProcessHandler<TId>.ExecuteParametersDto(
                            p.serviceTaskTokenAction.HandlerKey,
                            p.serviceTaskTokenAction.Id,
                            p.process,
                            p.component),
                        t);

                    await p.This.CompleteActionsAsync(
                        p.process,
                        p.component,
                        p.token,
                        executeResult.CompleteActions,
                        t);

                    var needAsyncExecuting = p.This.ActivateActions(
                        p.process,
                        p.component,
                        p.token,
                        executeResult.ActivateActions);
                    
                    if (!executeResult.IsComplete)
                    {
                        // Действие не завершено - асинхронное выполнение продолжается.
                        return ActionResult.AsyncExecutingResult();
                    }

                    p.This._schemaProcessActionSetter.ServiceTaskSetter.SetStatus(
                        p.state, 
                        ServiceTaskActionState.StatusEnum.Complete);

                    await p.This._triggerStateService.RemoveTriggerActionCompleteAsync(
                        p.process,
                        p.serviceTaskTokenAction.Id,
                        t);

                    if (p.serviceTaskTokenAction.Transition.HasValue)
                    {
                        return TokenExecutionService<TId>.TransitionResult(p.serviceTaskTokenAction.Transition.Value);
                    }

                    // Если была активация, то значит есть что выполнять.
                    if (needAsyncExecuting)
                    {
                        return ActionResult.AsyncExecutingResult();
                    }

                    return ActionResult.EmptyResult();
                },
                completeHandler: static (p, t) => ValueTask.FromResult(ActionResult.EmptyResult()),
                cancellationToken
                );
        }

        private async ValueTask<ActionResult> InnerExecuteActionAsync(
            IProcessContainer<TId> process,
            ISchemaProcessComponent component,
            ISchemaProcessHandler<TId> processHandler,
            TokenDto token,
            ConditionTokenAction conditionTokenAction,
            ConditionActionStateComponent state,
            CancellationToken cancellationToken)
        {
            return await _schemaProcessActionSetter.ConditionSetter.OneOfStatusAsync(
                (This: this, processHandler, process, component, conditionTokenAction, token, state),
                state.Status,
                noActivatedHandler: static (p, t) => ValueTask.FromResult(ActionResult.EmptyResult()),
                checkConditionHandler: static async (p, t) => 
                {
                    var result = await p.processHandler.CheckConditionAsync(
                        new ISchemaProcessHandler<TId>.ExecuteParametersDto(
                            p.conditionTokenAction.CheckHandlerKey,
                            p.conditionTokenAction.Id,
                            p.process,
                            p.component),
                        t);

                    if (!result)
                    {
                        return ActionResult.EmptyResult();
                    }

                    p.This._schemaProcessActionSetter.ConditionSetter.SetStatus(
                        p.state,
                        ConditionActionStateComponent.StatusEnum.Complete);                    

                    var needAsyncExecuting = false;
                    if (p.conditionTokenAction.ActionHandlerKey is not null)
                    {
                        var executeResult = await p.processHandler.ExecuteConditionHandlerAsync(
                            new ISchemaProcessHandler<TId>.ExecuteParametersDto(
                                p.conditionTokenAction.ActionHandlerKey,
                                p.conditionTokenAction.Id,
                                p.process,
                                p.component),
                            t);

                        await p.This.CompleteActionsAsync(
                            p.process,
                            p.component,
                            p.token,
                            executeResult.CompleteActions,
                            t);

                        needAsyncExecuting = p.This.ActivateActions(
                            p.process,
                            p.component,
                            p.token,
                            executeResult.ActivateActions);
                    }

                    await p.This._triggerStateService.RemoveTriggerActionCompleteAsync(
                        p.process,
                        p.conditionTokenAction.Id,
                        t);

                    if (p.conditionTokenAction.Transition.HasValue)
                    {
                        return TransitionResult(p.conditionTokenAction.Transition.Value);
                    }

                    // Если была активация, то значит есть что выполнять.
                    if (needAsyncExecuting)
                    {
                        return ActionResult.AsyncExecutingResult();
                    }

                    return ActionResult.EmptyResult();
                },
                completeHandler: static (p, t) => ValueTask.FromResult(ActionResult.EmptyResult()),
                cancellationToken
                );
        }

        private async ValueTask<ActionResult> InnerExecuteActionAsync(
            IProcessContainer<TId> process,
            ISchemaProcessComponent component,
            ISchemaProcessHandler<TId> processHandler,
            TokenDto token,
            TimerTokenAction timerTokenAction,
            TimerActionStateComponent state,
            CancellationToken cancellationToken)
        {
            return await _schemaProcessActionSetter.TimerSetter.OneOfStatusAsync(
                (This: this, processHandler, process, component, token, timerTokenAction, state),
                state.Status,
                noActivatedHandler: static (p, t) => ValueTask.FromResult(ActionResult.EmptyResult()),
                creatingTimerHandler: static async (p, t) => 
                {
                    var key = Guid.NewGuid().ToString();
                    var date = p.This._dateTimeProvider.UtcNow + p.timerTokenAction.Duration;

                    p.state.TriggerKey = key;
                    p.This._schemaProcessActionSetter.TimerSetter.SetTimerDate(
                        p.state,
                        date);

                    p.This._schemaProcessActionSetter.TimerSetter.SetStatus(
                        p.state,
                        TimerActionStateComponent.StatusEnum.WaitingTimer);
                    
                    await p.This._triggerRepository.CreateTriggerAsync(
                        ITriggerRepository<TId>.CreateTriggerDto.TimerTrigger(
                            key,
                            date,
                            p.process.Id,
                            isRangeTrigger: true,
                            handlerKey: p.This._options.TimerTriggerHandler,
                            priority:
                            p.process.Process.Info.Priority,
                            isActivated: true,
                            isChildTrigger: true),
                        t);

                    if (p.component.ProcessState is IProcessStateWithTriggers processStateWithTriggers)
                    {
                        processStateWithTriggers.TriggerState.Triggers.Add(
                            key,
                            new TriggerStateContainer.TriggerInfo(
                                key: key,
                                isStreamTrigger: false,
                                removeIfActionComplete: true,
                                removeActionIds: [p.timerTokenAction.Id],
                                removeTokenId: p.component.CurrentTokenId,
                                removeIfTokenMove: true,
                                removeIfProcessComplete: true));
                    }

                    return ActionResult.EmptyResult();
                },
                waitingTimerHandler: static async (p, t) => 
                {
                    _ = p.state.Date 
                        ?? throw new Exception("[Bug] Дата таймера не может быть пустой у взведенного таймера.");

                    // 1) Условие выполнения действия.
                    var condition = 
                        p.This._dateTimeProvider.UtcNow >= p.state.Date.Value;

                    if (!condition)
                    {
                        return ActionResult.EmptyResult();
                    }

                    p.This._schemaProcessActionSetter.TimerSetter.SetStatus(
                        p.state,
                        TimerActionStateComponent.StatusEnum.Complete);                    

                    if (p.state.TriggerKey is not null)
                    {
                        await p.This._triggerStateService.RemoveTriggerAsync(
                            p.process, 
                            p.state.TriggerKey, 
                            removeEvent: false, // Если мы попали сюда, то значит таймер сработал - событие не нужно.
                            t);
                        p.state.TriggerKey = null;
                    }
                    
                    // 2) Хендлер.
                    var needAsyncExecuting = false;
                    if (p.timerTokenAction.HandlerKey is not null)
                    {
                        var executeResult = await p.processHandler.ExecuteTimerAsync(
                            new ISchemaProcessHandler<TId>.ExecuteParametersDto(
                                p.timerTokenAction.HandlerKey,
                                p.timerTokenAction.Id,
                                p.process,
                                p.component),
                            t);

                        await p.This.CompleteActionsAsync(
                            p.process, 
                            p.component, 
                            p.token, 
                            executeResult.CompleteActions,
                            t);

                        needAsyncExecuting = p.This.ActivateActions(
                            p.process,
                            p.component,
                            p.token,                            
                            executeResult.ActivateActions);
                    }

                    if (p.timerTokenAction.Transition.HasValue)
                    {
                        return TransitionResult(p.timerTokenAction.Transition.Value);
                    }

                    // 3) Если была активация, то значит есть что выполнять.
                    if (needAsyncExecuting)
                    {
                        return ActionResult.AsyncExecutingResult();
                    }

                    return ActionResult.EmptyResult();
                },
                completeHandler: static (p, t) => ValueTask.FromResult(ActionResult.EmptyResult()),
                cancellationToken
                );
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

            static async ValueTask NotifySteamTriggersAsync(
                TokenExecutionService<TId> This,
                IProcessContainer<TId> process,
                ISchemaProcessComponent component,
                CancellationToken cancellationToken)
            {
                var streamTriggerKeys = Array.Empty<string>();
                switch (This._options.NotifyStreamTrigggersPolicy)
                {
                    case NotifyStreamTrigggersPolicy.No:
                        break;

                    case NotifyStreamTrigggersPolicy.SelectFromDb:
                        {
                            // Считываем триггеры из БД.
                            streamTriggerKeys = await This._queries
                                .GetStreamTriggerKeysByProcessRangeAsync(process.Id, cancellationToken);

                            break;
                        }

                    case NotifyStreamTrigggersPolicy.FromProcessStateWithTriggers:
                        {
                            if (component.ProcessState is IProcessStateWithTriggers processStateWithTriggers)
                            {
                                streamTriggerKeys = processStateWithTriggers.TriggerState.Triggers.Values
                                    .Where(e => e.IsStreamTrigger)
                                    .Select(e => e.Key)
                                    .ToArray();
                            }

                            break;
                        }

                    default:
                        throw new NotImplementedException(
                            This._options.NotifyStreamTrigggersPolicy.ToString());
                }

                if (streamTriggerKeys.Any())
                {
                    var streamTriggerComponent = new StreamTriggerComponent(This._options.GoWaitTriggerQueueName, streamTriggerKeys);
                    process.AddComponent<IStreamTriggerComponent>(streamTriggerComponent);
                }
            }

            // 1) Перемещение токена.
            if (result.MoveTokenId is not null)
            {
                // Автоматическое удаление триггеров при изменении токена.
                await _triggerStateService.RemoveTriggersMoveToken(
                    process,
                    component.CurrentTokenId,
                    cancellationToken);

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

            // 2) Завершение процесса.
            if (result.IsComplete)
            {
                // Автоматическое удаление триггеров при завершении процесса.
                await _triggerStateService.RemoveTriggersProcessCompleteAsync(process, cancellationToken);

                // Завершение процесса.
                component.MoveToken(
                    component.CurrentTokenId);
                _processSetter.SetStatus(process, ProcessStatusEnum.Complete);

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

                    // Оповщение stream триггеров о том, что процесс перешел в состояние ожидания внешнего сигнала.
                    await NotifySteamTriggersAsync(this, process, component, cancellationToken);
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
                }
            }
        }

        /// <summary>
        /// Вызывается перед выполнением действия.
        /// </summary>
        /// <returns>Нужна ли действию обработка асинхронного выполнения.</returns>
        private bool PrepareForExecuteAction(
            IProcessContainer<TId> process,
            ISchemaProcessComponent processData,
            ITokenAction tokenAction)
        {
            return _schemaProcessActionSetter.CommonSetter.OneOfWithState(
                1,
                processData,
                tokenAction,
                activateIfCreate: false,
                serviceTaskHandler: static (p, action, actionState) => 
                    actionState.Status 
                    is ServiceTaskActionState.StatusEnum.Executing,
                conditionHandler: static (p, action, actionState) => 
                    actionState.Status 
                    is ConditionActionStateComponent.StatusEnum.CheckCondition,
                timerHandler: static (p, action, actionState) => 
                    actionState.Status 
                    is TimerActionStateComponent.StatusEnum.CreatingTimer 
                    or TimerActionStateComponent.StatusEnum.WaitingTimer
                    );
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

        /// <summary>
        /// Активировать указанные действия (если они не активированы).
        /// </summary>
        /// <returns></returns>
        private bool ActivateActions(
            IProcessContainer<TId> process,
            ISchemaProcessComponent component,
            TokenDto token,            
            ISchemaProcessHandler.ActivateActionDto[] activateActions)
        {
            var needAsyncExecute = LinkContainer.Create(false);

            foreach (var elem in activateActions)
            {
                //if (!tokenAction.ActivateRelations.ContainsKey(elem))
                //{
                //    throw new Exception(
                //        $"Действие пытается активировать действие, которое не задекларировано. {token.Id}. {elem}.");
                //}

                var activateAction = token.GetAction(elem.ActionId);

                _schemaProcessActionSetter.CommonSetter.OneOfWithState(
                    (_schemaProcessActionSetter, elem, needAsyncExecute),
                    component,
                    activateAction,
                    activateIfCreate: true, // Активируем состояние, если создаем новое.
                    serviceTaskHandler: static (p, action, actionState) => 
                    {
                        p._schemaProcessActionSetter.ServiceTaskSetter.OneOfStatus(
                            (p._schemaProcessActionSetter.ServiceTaskSetter, p.needAsyncExecute, actionState),
                            actionState.Status,
                            noActivatedHandler: static (p) => 
                            {
                                // В1. Действие запущено - выполняемся.
                                p.ServiceTaskSetter.SetStatus(p.actionState, ServiceTaskActionState.StatusEnum.Executing);
                                p.needAsyncExecute.Data = true;

                                return 1;
                            },
                            executingHandler: static (p) => 
                            {
                                // Действие запущено - выполняемся.
                                p.needAsyncExecute.Data = true;

                                return 1;
                            },
                            completeHandler: static (p) => 
                            {
                                // В1. Действие запущено - выполняемся.
                                p.ServiceTaskSetter.SetStatus(p.actionState, ServiceTaskActionState.StatusEnum.Executing);
                                p.needAsyncExecute.Data = true;

                                return 1;
                            }
                            );

                        return 1;
                    },
                    conditionHandler: static (p, action, actionState) => 
                    {
                        p._schemaProcessActionSetter.ConditionSetter.OneOfStatus(
                            (p._schemaProcessActionSetter.ConditionSetter, p.needAsyncExecute, actionState, p.elem),
                            actionState.Status,
                            noActivatedHandler: static (p) => 
                            {
                                p.ConditionSetter.SetStatus(p.actionState, ConditionActionStateComponent.StatusEnum.CheckCondition);
                                // В1. Нужна ли проверка сейчас - как указал параметр.
                                p.needAsyncExecute.Data = p.needAsyncExecute.Data || p.elem.AsyncExecuteOrWaitSignal;

                                return 1;
                            },
                            checkConditionHandler: static (p) => 
                            {
                                // В2. Уже активировано, нужна ли проверка сейчас - как указал параметр.
                                p.needAsyncExecute.Data = p.needAsyncExecute.Data || p.elem.AsyncExecuteOrWaitSignal;

                                return 1;
                            },
                            completeHandler: static (p) => 
                            {
                                p.ConditionSetter.SetStatus(p.actionState, ConditionActionStateComponent.StatusEnum.CheckCondition);
                                // В1. Нужна ли проверка сейчас - как указал параметр.
                                p.needAsyncExecute.Data = p.needAsyncExecute.Data || p.elem.AsyncExecuteOrWaitSignal;

                                return 1;
                            });

                        return 1;
                    },
                    timerHandler: static (p, action, actionState) => 
                    {
                        p._schemaProcessActionSetter.TimerSetter.OneOfStatus(
                            (p._schemaProcessActionSetter.TimerSetter, p.needAsyncExecute, actionState),
                            actionState.Status,
                            noActivatedHandler: static (p) => 
                            {
                                // В1. Создаем таймер - асинхронное выполнение.
                                p.TimerSetter.SetStatus(p.actionState, TimerActionStateComponent.StatusEnum.CreatingTimer);
                                p.needAsyncExecute.Data = true;

                                return 1;
                            },
                            creatingTimerHandler: static (p) => 
                            {
                                // В2. Таймер создается - асинхронное выполнение.
                                p.needAsyncExecute.Data = true;

                                return 1;
                            },
                            waitingTimerHandler: static (p) => 
                            {
                                // В3. Таймер уже создан, пусть ждет сигнал.
                                p.needAsyncExecute.Data = p.needAsyncExecute.Data || false;

                                return 1;
                            },
                            completeHandler: static (p) => 
                            {
                                // В1. Создаем таймер - асинхронное выполнение.
                                p.TimerSetter.SetStatus(p.actionState, TimerActionStateComponent.StatusEnum.CreatingTimer);
                                p.needAsyncExecute.Data = true;

                                return 1;
                            });

                        return 1;
                    }
                    );
            }

            return needAsyncExecute.Data;
        }

        /// <summary>
        /// Завершить указанные действия (если они выполняются).
        /// Хендлер действия не вызывается.
        /// </summary>
        private async ValueTask CompleteActionsAsync(
            IProcessContainer<TId> process,
            ISchemaProcessComponent component,
            TokenDto token,            
            ISchemaProcessHandler.CompleteActionDto[] completeActions,
            CancellationToken cancellationToken)
        {
            // TODO: валидация возможности завершения.

            foreach (var elem in completeActions)
            {
                var action = token.GetAction(elem.ActionId);
                await _schemaProcessActionSetter.CommonSetter.OneOfWithStateAsync(
                    (This: this, process, component, elem),
                    component,
                    action,
                    activateIfCreate: false,
                    serviceTaskHandler: static async (p, action, state, t) =>
                    {
                        await p.This._schemaProcessActionSetter.ServiceTaskSetter.OneOfStatusAsync(
                            (This: p.This, p.process, p.component, p.elem, state), 
                            state.Status,
                            noActivatedHandler: static (p, t) => ValueTask.FromResult(1),
                            executingHandler: static async (p, t) => 
                            {
                                p.This._schemaProcessActionSetter.ServiceTaskSetter.SetStatus(p.state, ServiceTaskActionState.StatusEnum.Complete);
                                await p.This._triggerStateService.RemoveTriggerActionCompleteAsync(p.process, p.state.Id, t);

                                return 1;
                            },
                            completeHandler: static (p, t) => ValueTask.FromResult(1),
                            t);
                        return 1;
                    },
                    conditionHandler: static async (p, action, state, t) =>
                    {
                        await p.This._schemaProcessActionSetter.ConditionSetter.OneOfStatusAsync(
                            (This: p.This, p.process, p.component, p.elem, state),
                            state.Status,
                            noActivatedHandler: static (p, t) => ValueTask.FromResult(1),
                            checkConditionHandler: static async (p, t) => 
                            {
                                p.This._schemaProcessActionSetter.ConditionSetter.SetStatus(p.state, ConditionActionStateComponent.StatusEnum.Complete);
                                await p.This._triggerStateService.RemoveTriggerActionCompleteAsync(p.process, p.state.Id, t);

                                return 1;
                            },
                            completeHandler: static  (p, t) => ValueTask.FromResult(1),
                            t);

                        return 1;
                    },
                    timerHandler: static async (p, action, state, t) =>
                    {
                        await p.This._schemaProcessActionSetter.TimerSetter.OneOfStatusAsync(
                            (This: p.This, p.process, p.component, state, p.elem),
                            state.Status,
                            noActivatedHandler: static (p, t) => ValueTask.FromResult(1),
                            creatingTimerHandler: (p, t) =>
                            {
                                p.This._schemaProcessActionSetter.TimerSetter.SetStatus(p.state, TimerActionStateComponent.StatusEnum.Complete);
                                return ValueTask.FromResult(1);
                            },
                            waitingTimerHandler: static async (p, t) =>
                            {
                                p.This._schemaProcessActionSetter.TimerSetter.SetStatus(p.state, TimerActionStateComponent.StatusEnum.Complete);

                                if (p.state.TriggerKey != null)
                                {
                                    await p.This._triggerStateService.RemoveTriggerAsync(p.process, p.state.TriggerKey, removeEvent: true, t);
                                    p.state.TriggerKey = null;
                                }

                                return 1;
                            },
                            completeHandler: static (p, t) => ValueTask.FromResult(1),
                            t);

                        return 1;
                    },
                    cancellationToken
                    );
            }
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

            public required NotifyStreamTrigggersPolicy NotifyStreamTrigggersPolicy { get; set; }
        }

        public interface IQueries
        {
            Task<string[]> GetStreamTriggerKeysByProcessRangeAsync(
                TId processId,
                CancellationToken cancellationToken);
        }

        public enum NotifyStreamTrigggersPolicy
        {
            No,

            /// <summary>
            /// Считать перечень ключей экземпляров stream триггеров из БД по ProcessId.
            /// </summary>
            SelectFromDb,

            /// <summary>
            /// Получить перечень ключей экземпляров stream триггеров на основании <see cref="IProcessStateWithTriggers"/>.
            /// Для корректной работы метаданные всез триггеров должны быть записаны в process state.
            /// </summary>
            FromProcessStateWithTriggers
        }

        #endregion
    }
}
