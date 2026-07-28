using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Component.ActionComponent;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Service;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Implementation.Services
{
    public class SchemaProcessActionSetter
        : ISchemaProcessActionSetter
    {
        public ISchemaProcessActionSetter.ICommonSetter CommonSetter { get; }

        public ISchemaProcessActionSetter.IServiceTaskSetter ServiceTaskSetter { get; }

        public ISchemaProcessActionSetter.IConditionSetter ConditionSetter { get; }

        public ISchemaProcessActionSetter.ITimerSetter TimerSetter { get; }

        public SchemaProcessActionSetter(
            ISchemaProcessActionSetter.ICommonSetter oneOfSetter,
            ISchemaProcessActionSetter.IServiceTaskSetter serviceTaskSetter,
            ISchemaProcessActionSetter.IConditionSetter conditionSetter,
            ISchemaProcessActionSetter.ITimerSetter timerSetter)
        {
            CommonSetter = oneOfSetter;
            ServiceTaskSetter = serviceTaskSetter;
            ConditionSetter = conditionSetter;
            TimerSetter = timerSetter;
        }

        #region types

        public class CommonSetterImpl 
            : ISchemaProcessActionSetter.ICommonSetter
        {
            public TokenActionKindEnum GetKind(ITokenAction tokenAction)
            {
                return tokenAction switch 
                {
                    ServiceTaskTokenAction => TokenActionKindEnum.ServiceTask,
                    ConditionTokenAction => TokenActionKindEnum.Condition,
                    TimerTokenAction => TokenActionKindEnum.Timer,

                    _ => throw new NotImplementedException(tokenAction.GetType().FullName)
                };
            }

            public TokenActionKindEnum GetKind(ITokenActionStateComponent tokenActionState)
            {
                return tokenActionState switch
                {
                    ServiceTaskActionState => TokenActionKindEnum.ServiceTask,
                    ConditionActionStateComponent => TokenActionKindEnum.Condition,
                    TimerActionStateComponent => TokenActionKindEnum.Timer,

                    _ => throw new NotImplementedException(tokenActionState.GetType().FullName)
                };
            }

            public TResult OneOfKind<TParameter, TResult>(
                TParameter paramter, 
                TokenActionKindEnum kind,
                Func<TParameter, TResult> serviceTaskHandler,
                Func<TParameter, TResult> conditionHandler,
                Func<TParameter, TResult> timerHandler)
            {
                return kind switch 
                {
                    TokenActionKindEnum.ServiceTask => serviceTaskHandler(paramter),
                    TokenActionKindEnum.Condition => conditionHandler(paramter),
                    TokenActionKindEnum.Timer => timerHandler(paramter),

                    _ => throw new NotImplementedException(kind.ToString())
                };
            }

            public TResult OneOf<TParameter, TResult>(
                TParameter parameter,
                ITokenAction tokenAction, 
                Func<TParameter, ServiceTaskTokenAction, TResult> serviceTaskHandler, 
                Func<TParameter, ConditionTokenAction, TResult> conditionHandler, 
                Func<TParameter, TimerTokenAction, TResult> timerHandler)
            {
                return tokenAction switch 
                {
                    ServiceTaskTokenAction serviceTaskTokenAction => serviceTaskHandler(parameter, serviceTaskTokenAction),
                    ConditionTokenAction conditionTokenAction => conditionHandler(parameter, conditionTokenAction),
                    TimerTokenAction timerTokenAction => timerHandler(parameter, timerTokenAction),

                    _ => throw new NotImplementedException(tokenAction.GetType().FullName)
                };
            }            

            public TResult OneOfWithState<TParameter, TResult>(
                TParameter parameter, 
                ISchemaProcessComponent schemaProcessComponent,
                ITokenAction tokenAction,
                bool activateIfCreate,
                Func<TParameter, ServiceTaskTokenAction, ServiceTaskActionState, TResult> serviceTaskHandler, 
                Func<TParameter, ConditionTokenAction, ConditionActionStateComponent, TResult> conditionHandler,
                Func<TParameter, TimerTokenAction, TimerActionStateComponent, TResult> timerHandler,
                Action<TParameter, ServiceTaskTokenAction>? serviceTaskNotExsistStateHandler = null,
                Action<TParameter, ConditionTokenAction>? conditionNotExsistStateHandler = null,
                Action<TParameter, TimerTokenAction>? timerTaskNotExsistStateHandler = null)
            {
                return tokenAction switch
                {
                    ServiceTaskTokenAction serviceTaskTokenAction => serviceTaskHandler(
                        parameter, 
                        serviceTaskTokenAction, 
                        GetOrCreateActionState(
                            parameter,
                            schemaProcessComponent, 
                            serviceTaskTokenAction, 
                            isActivate: activateIfCreate,
                            serviceTaskNotExsistStateHandler ?? ((_, _) => { })                                
                            )
                        ),
                    ConditionTokenAction conditionTokenAction => conditionHandler(
                        parameter, 
                        conditionTokenAction,
                        GetOrCreateActionState(
                            parameter,
                            schemaProcessComponent,
                            conditionTokenAction, 
                            isActivate: activateIfCreate,
                            conditionNotExsistStateHandler ?? ((_, _) => { })
                            )
                        ),
                    TimerTokenAction timerTokenAction => timerHandler(
                        parameter, 
                        timerTokenAction,
                        GetOrCreateActionState(
                            parameter,
                            schemaProcessComponent,
                            timerTokenAction,
                            isActivate: activateIfCreate,
                            timerTaskNotExsistStateHandler ?? ((_, _) => { })
                            )
                        ),

                    _ => throw new NotImplementedException(tokenAction.GetType().FullName)
                };
            }

            public async ValueTask<TResult> OneOfWithStateAsync<TParameter, TResult>(
                TParameter parameter, 
                ISchemaProcessComponent schemaProcessComponent,
                ITokenAction tokenAction,
                bool activateIfCreate,
                Func<TParameter, ServiceTaskTokenAction, ServiceTaskActionState, CancellationToken, ValueTask<TResult>> serviceTaskHandler,
                Func<TParameter, ConditionTokenAction, ConditionActionStateComponent, CancellationToken, ValueTask<TResult>> conditionHandler,
                Func<TParameter, TimerTokenAction, TimerActionStateComponent, CancellationToken, ValueTask<TResult>> timerHandler,
                CancellationToken cancellationToken)
            {
                return tokenAction switch
                {
                    ServiceTaskTokenAction serviceTaskTokenAction => await serviceTaskHandler(
                        parameter,
                        serviceTaskTokenAction,
                        GetOrCreateActionState(
                            parameter,
                            schemaProcessComponent, 
                            serviceTaskTokenAction, 
                            isActivate: activateIfCreate,
                            (_, _) => { }),
                        cancellationToken),
                    ConditionTokenAction conditionTokenAction => await conditionHandler(
                        parameter,
                        conditionTokenAction,
                        GetOrCreateActionState(
                            parameter,
                            schemaProcessComponent,
                            conditionTokenAction,
                            isActivate: activateIfCreate,
                            (_, _) => { }),
                        cancellationToken),
                    TimerTokenAction timerTokenAction => await timerHandler(
                        parameter,
                        timerTokenAction,
                        GetOrCreateActionState(
                            parameter,
                            schemaProcessComponent, 
                            timerTokenAction,
                            isActivate: activateIfCreate,
                            (_, _) => { }),
                        cancellationToken),

                    _ => throw new NotImplementedException(tokenAction.GetType().FullName)
                };
            }

            #region GetOrCreateActionState

            private ServiceTaskActionState GetOrCreateActionState<TParameter>(
                TParameter parameter,
                ISchemaProcessComponent processData,
                ServiceTaskTokenAction tokenAction,
                bool isActivate,
                Action<TParameter, ServiceTaskTokenAction> notExistsHandler)
            {
                // Существующий.
                if (processData.TryGetActionState<ServiceTaskActionState>(tokenAction.Id, out var state))
                {
                    return state;
                }

                // Новый.
                notExistsHandler(parameter, tokenAction);

                var status = ServiceTaskActionState.StatusEnum.NoActivated;
                if (tokenAction.ActivatedOnStart || isActivate)
                {
                    status = ServiceTaskActionState.StatusEnum.Executing;
                }
                
                state = new ServiceTaskActionState(
                    tokenAction.Id,
                    status);
                processData.AddActionState(state);

                return state;
            }

            private ConditionActionStateComponent GetOrCreateActionState<TParameter>(
                TParameter parameter,
                ISchemaProcessComponent processData,
                ConditionTokenAction tokenAction,
                bool isActivate,
                Action<TParameter, ConditionTokenAction> notExistsHandler)
            {
                // Существующий.
                if (processData.TryGetActionState<ConditionActionStateComponent>(tokenAction.Id, out var state))
                {
                    return state;
                }

                // Новый.
                notExistsHandler(parameter, tokenAction);

                var status = ConditionActionStateComponent.StatusEnum.NoActivated;
                if (tokenAction.ActivatedOnStart || isActivate)
                {
                    status = ConditionActionStateComponent.StatusEnum.CheckCondition;
                }

                state = new ConditionActionStateComponent(
                    tokenAction.Id,
                    status);
                processData.AddActionState(state);

                return state;
            }

            private TimerActionStateComponent GetOrCreateActionState<TParameter>(
                TParameter parameter,
                ISchemaProcessComponent processData,
                TimerTokenAction tokenAction,
                bool isActivate,
                Action<TParameter, TimerTokenAction> notExistsHandler)
            {
                // Существующий.
                if (processData.TryGetActionState<TimerActionStateComponent>(tokenAction.Id, out var state))
                {
                    return state;
                }

                // Новый.
                notExistsHandler(parameter, tokenAction);

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
            
            #endregion
        }

        public class ServiceTaskSetterImpl 
            : ISchemaProcessActionSetter.IServiceTaskSetter
        {
            public TResult OneOfStatus<TParameter, TResult>(
                TParameter parameter,
                ServiceTaskActionState.StatusEnum status, 
                Func<TParameter, TResult> noActivatedHandler, 
                Func<TParameter, TResult> executingHandler,
                Func<TParameter, TResult> completeHandler)
            {
                return status switch 
                {
                    ServiceTaskActionState.StatusEnum.NoActivated => noActivatedHandler(parameter),
                    ServiceTaskActionState.StatusEnum.Executing => executingHandler(parameter),
                    ServiceTaskActionState.StatusEnum.Complete => completeHandler(parameter),

                    _ => throw new NotImplementedException(status.ToString())
                };
            }

            public async ValueTask<TResult> OneOfStatusAsync<TParameter, TResult>(
                TParameter parameter, 
                ServiceTaskActionState.StatusEnum status, 
                Func<TParameter, CancellationToken, ValueTask<TResult>> noActivatedHandler, 
                Func<TParameter, CancellationToken, ValueTask<TResult>> executingHandler, 
                Func<TParameter, CancellationToken, ValueTask<TResult>> completeHandler, 
                CancellationToken cancellationToken)
            {
                return status switch
                {
                    ServiceTaskActionState.StatusEnum.NoActivated => await noActivatedHandler(parameter, cancellationToken),
                    ServiceTaskActionState.StatusEnum.Executing => await executingHandler(parameter, cancellationToken),
                    ServiceTaskActionState.StatusEnum.Complete => await completeHandler(parameter, cancellationToken),

                    _ => throw new NotImplementedException(status.ToString())
                };
            }

            public void SetStatus(
                ServiceTaskActionState timerActionState,
                ServiceTaskActionState.StatusEnum status)
            {
                timerActionState.Status = status;
            }
        }

        public class ConditionSetterImpl
            : ISchemaProcessActionSetter.IConditionSetter
        {
            public TResult OneOfStatus<TParameter, TResult>(
                TParameter parameter, 
                ConditionActionStateComponent.StatusEnum status,
                Func<TParameter, TResult> noActivatedHandler, 
                Func<TParameter, TResult> checkConditionHandler,
                Func<TParameter, TResult> completeHandler)
            {
                return status switch 
                {
                    ConditionActionStateComponent.StatusEnum.NoActivated => noActivatedHandler(parameter),
                    ConditionActionStateComponent.StatusEnum.CheckCondition => checkConditionHandler(parameter),
                    ConditionActionStateComponent.StatusEnum.Complete => completeHandler(parameter),

                    _ => throw new NotImplementedException(status.ToString())
                };
            }

            public async ValueTask<TResult> OneOfStatusAsync<TParameter, TResult>(
                TParameter parameter, 
                ConditionActionStateComponent.StatusEnum status,
                Func<TParameter, CancellationToken, ValueTask<TResult>> noActivatedHandler, 
                Func<TParameter, CancellationToken, ValueTask<TResult>> checkConditionHandler, 
                Func<TParameter, CancellationToken, ValueTask<TResult>> completeHandler, 
                CancellationToken cancellationToken)
            {
                return status switch
                {
                    ConditionActionStateComponent.StatusEnum.NoActivated => await noActivatedHandler(parameter, cancellationToken),
                    ConditionActionStateComponent.StatusEnum.CheckCondition => await checkConditionHandler(parameter, cancellationToken),
                    ConditionActionStateComponent.StatusEnum.Complete => await completeHandler(parameter, cancellationToken),

                    _ => throw new NotImplementedException(status.ToString())
                };
            }

            public void SetStatus(
                ConditionActionStateComponent conditionActionState, 
                ConditionActionStateComponent.StatusEnum status)
            {
                conditionActionState.Status = status;
            }
        }

        public class TimerSetterImpl : ISchemaProcessActionSetter.ITimerSetter
        {
            public TResult OneOfStatus<TParameter, TResult>(
                TParameter parameter,
                TimerActionStateComponent.StatusEnum status,
                Func<TParameter, TResult> noActivatedHandler,
                Func<TParameter, TResult> creatingTimerHandler,
                Func<TParameter, TResult> waitingTimerHandler,
                Func<TParameter, TResult> completeHandler)
            {
                return status switch
                {
                    TimerActionStateComponent.StatusEnum.NoActivated => noActivatedHandler(parameter),
                    TimerActionStateComponent.StatusEnum.CreatingTimer => creatingTimerHandler(parameter),
                    TimerActionStateComponent.StatusEnum.WaitingTimer => waitingTimerHandler(parameter),
                    TimerActionStateComponent.StatusEnum.Complete => completeHandler(parameter),

                    _ => throw new NotImplementedException(status.ToString())
                };
            }

            public async ValueTask<TResult> OneOfStatusAsync<TParameter, TResult>(
                TParameter parameter, 
                TimerActionStateComponent.StatusEnum status,
                Func<TParameter, CancellationToken, ValueTask<TResult>> noActivatedHandler,
                Func<TParameter, CancellationToken, ValueTask<TResult>> creatingTimerHandler, 
                Func<TParameter, CancellationToken, ValueTask<TResult>> waitingTimerHandler,
                Func<TParameter, CancellationToken, ValueTask<TResult>> completeHandler, 
                CancellationToken cancellationToken)
            {
                return status switch
                {
                    TimerActionStateComponent.StatusEnum.NoActivated => await noActivatedHandler(parameter, cancellationToken),
                    TimerActionStateComponent.StatusEnum.CreatingTimer => await creatingTimerHandler(parameter, cancellationToken),
                    TimerActionStateComponent.StatusEnum.WaitingTimer => await waitingTimerHandler(parameter, cancellationToken),
                    TimerActionStateComponent.StatusEnum.Complete => await completeHandler(parameter, cancellationToken),

                    _ => throw new NotImplementedException(status.ToString())
                };
            }

            public void SetStatus(
                TimerActionStateComponent timerActionState, 
                TimerActionStateComponent.StatusEnum status)
            {
                timerActionState.Status = status;
            }

            public void SetTimerDate(
                TimerActionStateComponent timerActionState,
                DateTimeOffset date)
            {
                timerActionState.Date = date;
            }
        }

        #endregion
    }
}
