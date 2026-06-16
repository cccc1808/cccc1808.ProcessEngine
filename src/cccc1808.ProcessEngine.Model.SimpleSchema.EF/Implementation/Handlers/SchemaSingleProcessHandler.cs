using System;
using System.Diagnostics;
using System.Threading;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component.ActionComponent;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Service;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Handlers
{
    public class SchemaSingleProcessHandler<TId> 
        : BaseSingleProcessHandler<TId>
    {        
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEFDbContext _dbContext;
        private readonly ISchemaService<TId> _schemaService;

        public SchemaSingleProcessHandler(
            IIsolationService isolationService,
            IProcessRepository<TId> repository,
            ITriggerRepository<TId> triggerRepository,
            IProcessSetter processSetter,            
            IDateTimeProvider dateTimeProvider,
            IEFDbContext dbContext,
            ISchemaService<TId> schemaService)
            : base(
                  isolationService,
                  repository,
                  triggerRepository,
                  processSetter)
        {
            _dateTimeProvider = dateTimeProvider;
            _dbContext = dbContext;
            _schemaService = schemaService;            
        }

        protected override OptionsDto SingleOptions 
            => Presets<TId>.Preset1_Single;

        protected override async ValueTask StepAsync(
            IProcessContainer<TId> process,
            CancellationToken cancellationToken)
        {
            var processData = process.GetComponent<ISchemaProcessComponent>();
            var processHandler = _schemaService.GetProcessHandler(process.Process.Info.ProcessType);
            var token = await _schemaService.GetSchemaToken(process.Process.Info.ProcessType, processData.CurrentTokenId, cancellationToken);

            var moveTokenId = (string?)null;
            var isAsyncExecuting = false;
            var isComplete = false;

            foreach (var elem in token.Actions)
            {
                var br = false;

                switch (elem)
                {
                    case TimerTokenAction timerTokenAction:
                        {
                            if (!processData.TryGetActionState<TimerActionStateComponent>(elem.Id, out var state))
                            {
                                var date = _dateTimeProvider.UtcNow + timerTokenAction.Duration;
                                state = new TimerActionStateComponent(elem.Id, date, isComplete: false);
                                processData.AddActionState(state);

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
                                if (state.IsComplete)
                                {
                                    break;
                                }

                                if (_dateTimeProvider.UtcNow >= state.Date)
                                {
                                    if (timerTokenAction.HandlerKey is not null)
                                    {
                                        var needRepeat = await processHandler.ExecuteTimerAsync(
                                            timerTokenAction.HandlerKey, 
                                            elem.Id,
                                            process,
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
                                        if (timerTokenAction.Transition.Value.IsComplete)
                                        {
                                            isComplete = true;
                                        }
                                        else
                                        {
                                            moveTokenId = timerTokenAction.Transition.Value.TargetTokenId;
                                            isAsyncExecuting = true;
                                        }

                                        br = true;
                                    }
                                }
                            }

                            break;
                        }

                    case ConditionTokenAction conditionTokenAction:
                        {
                            if (!processData.TryGetActionState<ConditionActionStateComponent>(elem.Id, out var state))
                            {
                                state = new ConditionActionStateComponent(elem.Id, isComplete: false);
                                processData.AddActionState(state);
                            }

                            if (state.IsComplete)
                            {
                                break;
                            }

                            {
                                var result = await processHandler.CheckConditionAsync(
                                    conditionTokenAction.CheckHandlerKey,
                                    elem.Id,
                                    process, 
                                    cancellationToken);

                                if (result)
                                {
                                    if (conditionTokenAction.ActionHandlerKey is not null)
                                    {
                                        await processHandler.ExecuteConditionHandlerAsync(
                                            conditionTokenAction.ActionHandlerKey,
                                            elem.Id,
                                            process,
                                            cancellationToken);

                                        state.IsComplete = true;
                                    }

                                    if (conditionTokenAction.Transition.HasValue)
                                    {
                                        if (conditionTokenAction.Transition.Value.IsComplete)
                                        {
                                            isComplete = true;
                                        }
                                        else
                                        {
                                            moveTokenId = conditionTokenAction.Transition.Value.TargetTokenId;
                                            isAsyncExecuting = true;
                                        }

                                        br = true;
                                    }                                    
                                }
                            }
                            
                            break;
                        }

                    case ServiceTaskTokenAction serviceTaskTokenAction:
                        {
                            if (!processData.TryGetActionState<ServiceTaskActionState>(elem.Id, out var state))
                            {
                                state = new ServiceTaskActionState(elem.Id, isComplete: false);
                                processData.AddActionState(state);
                            }

                            if (!state.IsComplete)
                            {
                                state.IsComplete = await processHandler.ExecuteServiceTask(
                                    serviceTaskTokenAction.HandlerKey,
                                    elem.Id,
                                    process,
                                    cancellationToken);
                            }

                            if (!state.IsComplete)
                            {
                                isAsyncExecuting = true;
                            }
                            else
                            {
                                if (serviceTaskTokenAction.Transition.HasValue)
                                {
                                    if (serviceTaskTokenAction.Transition.Value.IsComplete)
                                    {
                                        isComplete = true;
                                    }
                                    else
                                    {
                                        moveTokenId = serviceTaskTokenAction.Transition.Value.TargetTokenId;
                                        isAsyncExecuting = true;
                                    }

                                    br = true;
                                }                                
                            }

                            break;
                        }

                    default:
                        throw new NotImplementedException(elem.GetType().FullName);
                }

                if (br)
                {
                    break;
                }
            }

            if (moveTokenId != null)
            {
                // Продолжение асинхронного выполнения на другом токене.
                processData.ClearActionStates();
                processData.CurrentTokenId = moveTokenId;
            }
            else 
            {
                if (isComplete)
                {
                    // Завершение процесса.
                    processData.ClearActionStates();
                    _processSetter.SetStatus(process, ProcessStatusEnum.Complete);
                }
                else if (!isAsyncExecuting)
                {
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
                else 
                {
                    // Продолжение асинхронного выполнения на текущем токене.
                }
            }
        }
    }
}
