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
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component.ActionComponent;
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
                    elem, 
                    cancellationToken);

                actionsResult = actionsResult.MergeFrom(actionResult);

                if (actionsResult.IsBreak)
                {
                    break;
                }
            }

            await SetActionResultAsync(
                process,
                processData,
                actionsResult,
                isAsyncExecuting: true,
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
                token.GetAction(actionId),
                cancellationToken
                );

            await SetActionResultAsync(
                process, 
                processData,
                actionResult,
                isAsyncExecuting: false,
                cancellationToken);
        }  
        
        private async ValueTask<ActionResult> InnerExecuteActionAsync(
            IProcessContainer<TId> process,
            ISchemaProcessComponent component,
            ISchemaProcessHandler<TId> processHandler,
            ITokenAction tokenAction,
            CancellationToken cancellationToken)
        {
            static ActionResult TransitionResult(ITokenAction.TransitionDto transition)
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

            switch (tokenAction)
            {
                case TimerTokenAction timerTokenAction:
                    {
                        if (!component.TryGetActionState<TimerActionStateComponent>(tokenAction.Id, out var state))
                        {
                            var date = _dateTimeProvider.UtcNow + timerTokenAction.Duration;
                            state = new TimerActionStateComponent(tokenAction.Id, date, isComplete: false);
                            component.AddActionState(state);

                            await _triggerRepository.CreateTriggerAsync(
                                ITriggerRepository<TId>.CreateTriggerDto.TimerTrigger(
                                    Guid.NewGuid().ToString(),
                                    date,
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
                        else
                        {
                            // Дествие заверщшено.
                            if (state.IsComplete)
                            {
                                return ActionResult.EmptyResult();
                            }

                            // Условие выполнения действия.
                            var condition = _dateTimeProvider.UtcNow >= state.Date;

                            if (!condition)
                            {
                                return ActionResult.EmptyResult();
                            }

                            if (timerTokenAction.HandlerKey is not null)
                            {
                                var needRepeat = await processHandler.ExecuteTimerAsync(
                                    new ISchemaProcessHandler<TId>.ExecuteParametersDto(
                                        timerTokenAction.HandlerKey,
                                        tokenAction.Id,
                                        process,
                                        component),
                                    cancellationToken);

                                if (needRepeat)
                                {
                                    var date = _dateTimeProvider.UtcNow + timerTokenAction.Duration;
                                    state.Date = date;

                                    await _triggerRepository.CreateTriggerAsync(
                                        ITriggerRepository<TId>.CreateTriggerDto.TimerTrigger(
                                            Guid.NewGuid().ToString(),
                                            date,
                                            process.Id,
                                            isRangeTrigger: true,
                                            handlerKey: EFTimerChildTriggerHandler<TId>.Name,
                                            priority:
                                            process.Process.Info.Priority,
                                            isActivated: true,
                                            isChildTrigger: true),
                                        cancellationToken);
                                }
                                else
                                {
                                    state.IsComplete = true;
                                }
                            }

                            if (timerTokenAction.Transition.HasValue)
                            {
                                return TransitionResult(timerTokenAction.Transition.Value);
                            }

                            return ActionResult.EmptyResult();
                        }
                    }

                case ConditionTokenAction conditionTokenAction:
                    {
                        if (!component.TryGetActionState<ConditionActionStateComponent>(tokenAction.Id, out var state))
                        {
                            state = new ConditionActionStateComponent(tokenAction.Id, isComplete: false);
                            component.AddActionState(state);
                        }

                        if (state.IsComplete)
                        {
                            return ActionResult.EmptyResult();
                        }

                        {
                            var result = await processHandler.CheckConditionAsync(
                                new ISchemaProcessHandler<TId>.ExecuteParametersDto(
                                    conditionTokenAction.CheckHandlerKey,
                                    tokenAction.Id,
                                    process,
                                    component),
                                cancellationToken);

                            if (!result)
                            {
                                return ActionResult.EmptyResult();
                            }

                            if (conditionTokenAction.ActionHandlerKey is not null)
                            {
                                await processHandler.ExecuteConditionHandlerAsync(
                                    new ISchemaProcessHandler<TId>.ExecuteParametersDto(
                                        conditionTokenAction.ActionHandlerKey,
                                        tokenAction.Id,
                                        process,
                                        component),
                                    cancellationToken);

                                state.IsComplete = true;
                            }

                            if (conditionTokenAction.Transition.HasValue)
                            {
                                return TransitionResult(conditionTokenAction.Transition.Value);
                            }

                            return ActionResult.EmptyResult();
                        }
                    }

                case ServiceTaskTokenAction serviceTaskTokenAction:
                    {
                        if (!component.TryGetActionState<ServiceTaskActionState>(tokenAction.Id, out var state))
                        {
                            state = new ServiceTaskActionState(tokenAction.Id, isComplete: false);
                            component.AddActionState(state);
                        }

                        if (!state.IsComplete)
                        {
                            state.IsComplete = await processHandler.ExecuteServiceTask(
                                new ISchemaProcessHandler<TId>.ExecuteParametersDto(
                                    serviceTaskTokenAction.HandlerKey,
                                    tokenAction.Id,
                                    process,
                                    component),
                                cancellationToken);
                        }

                        if (!state.IsComplete)
                        {
                            return ActionResult.AsyncExecutingResult();
                        }
                        else
                        {
                            if (serviceTaskTokenAction.Transition.HasValue)
                            {
                                return TransitionResult(serviceTaskTokenAction.Transition.Value);
                            }

                            return ActionResult.EmptyResult();
                        }                        
                    }

                default:
                    throw new NotImplementedException(tokenAction.GetType().FullName);
            }
        }

        private async ValueTask SetActionResultAsync(
           IProcessContainer<TId> process,
           ISchemaProcessComponent processData,
           ActionResult result,
           bool isAsyncExecuting,
           CancellationToken cancellationToken)
        {
            if (result.MoveTokenId is not null)
            {
                // Меняем токен.
                processData.MoveToken(
                    result.MoveTokenId);

                if (!isAsyncExecuting)
                {
                    // Продолжение асинхронного выполнения на другом токене.
                    _processSetter.SetStatus(process, ProcessStatusEnum.AsyncExecute);
                }

                return;
            }

            if (result.IsComplete)
            {
                // Завершение процесса.
                processData.MoveToken(
                    processData.CurrentTokenId);
                _processSetter.SetStatus(process, ProcessStatusEnum.Complete);

                return;
            }

            // Вызов из асинхронного выполнения.
            if (isAsyncExecuting)
            {
                if (result.IsAsyncExecuting)
                {
                    // Продолжение асинхронного выполнения на текущем токене.
                    return;
                }

                var haveNotComplete = processData.AllActionStates()
                    .OfType<ServiceTaskActionState>()
                    .Any(e => !e.IsComplete);

                if (!haveNotComplete)
                {
                    // Ожидание сигнала.
                    _processSetter.SetStatus(process, ProcessStatusEnum.WaitEvent);

                    // TODO:
                    if (processData.AutoDetectStreamTriggers)
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
                            var component = new StreamTriggerComponent("trigger_events", streamTriggerKeys);
                            process.AddComponent<IStreamTriggerComponent>(component);
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
                var haveNotComplete = false;

                if (!result.IsAsyncExecuting)
                {
                    haveNotComplete = processData.AllActionStates()
                        .OfType<ServiceTaskActionState>()
                        .Any(e => !e.IsComplete);
                }

                if (result.IsAsyncExecuting || haveNotComplete)
                {
                    // Переход в асинхронное выполнение.
                    _processSetter.SetStatus(process, ProcessStatusEnum.AsyncExecute);
                }
                else
                {
                    // Продолжение ожидания сигнала.
                }
            }
        }

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
