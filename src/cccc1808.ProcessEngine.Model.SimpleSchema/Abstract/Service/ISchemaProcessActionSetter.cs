using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Component.ActionComponent;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Dto.TokenActions;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Service
{
    public interface ISchemaProcessActionSetter
    {
        ICommonSetter CommonSetter { get; }

        IServiceTaskSetter ServiceTaskSetter { get; }

        IConditionSetter ConditionSetter { get; }

        ITimerSetter TimerSetter { get; }

        #region types

        public interface ICommonSetter
        {
            TokenActionKindEnum GetKind(
                ITokenAction tokenAction);

            TokenActionKindEnum GetKind(
                ITokenActionStateComponent tokenActionState);

            TResult OneOfKind<TParameter, TResult>(
                TParameter paramter,
                TokenActionKindEnum kind,
                Func<TParameter, TResult> serviceTaskHandler,
                Func<TParameter, TResult> conditionHandler,
                Func<TParameter, TResult> timerHandler);

            TResult OneOf<TParameter, TResult>(
                TParameter parameter,
                ITokenAction tokenAction,
                Func<TParameter, ServiceTaskTokenAction, TResult> serviceTaskHandler,
                Func<TParameter, ConditionTokenAction, TResult> conditionHandler,
                Func<TParameter, TimerTokenAction, TResult> timerHandler);            

            TResult OneOfWithState<TParameter, TResult>(
                TParameter parameter,
                ISchemaProcessComponent schemaProcessComponent,
                ITokenAction tokenAction,
                bool activateIfCreate,
                Func<TParameter, ServiceTaskTokenAction, ServiceTaskActionState, TResult> serviceTaskHandler,
                Func<TParameter, ConditionTokenAction, ConditionActionStateComponent, TResult> conditionHandler,
                Func<TParameter, TimerTokenAction, TimerActionStateComponent, TResult> timerHandler,
                Action<TParameter, ServiceTaskTokenAction>? serviceTaskNotExsistStateHandler = null,
                Action<TParameter, ConditionTokenAction>? conditionNotExsistStateHandler = null,
                Action<TParameter, TimerTokenAction>? timerTaskNotExsistStateHandler = null);

            ValueTask<TResult> OneOfWithStateAsync<TParameter, TResult>(
                TParameter parameter,
                ISchemaProcessComponent schemaProcessComponent,
                ITokenAction tokenAction,
                bool activateIfCreate,
                Func<TParameter, ServiceTaskTokenAction, ServiceTaskActionState, CancellationToken, ValueTask<TResult>> serviceTaskHandler,
                Func<TParameter, ConditionTokenAction, ConditionActionStateComponent, CancellationToken, ValueTask<TResult>> conditionHandler,
                Func<TParameter, TimerTokenAction, TimerActionStateComponent, CancellationToken, ValueTask<TResult>> timerHandler,
                CancellationToken cancellationToken);
        }

        public interface IServiceTaskSetter 
        {
            TResult OneOfStatus<TParameter, TResult>(
                TParameter parameter,
                ServiceTaskActionState.StatusEnum status,
                Func<TParameter, TResult> noActivatedHandler,
                Func<TParameter, TResult> executingHandler,
                Func<TParameter, TResult> completeHandler
                );

            ValueTask<TResult> OneOfStatusAsync<TParameter, TResult>(
                TParameter parameter,
                ServiceTaskActionState.StatusEnum status,
                Func<TParameter, CancellationToken, ValueTask<TResult>> noActivatedHandler,
                Func<TParameter, CancellationToken, ValueTask<TResult>> executingHandler,
                Func<TParameter, CancellationToken, ValueTask<TResult>> completeHandler,
                CancellationToken cancellationToken
                );

            void SetStatus(
                ServiceTaskActionState timerActionState,
                ServiceTaskActionState.StatusEnum status);
        }

        public interface IConditionSetter
        {
            TResult OneOfStatus<TParameter, TResult>(
                TParameter parameter,
                ConditionActionStateComponent.StatusEnum status,
                Func<TParameter, TResult> noActivatedHandler,
                Func<TParameter, TResult> checkConditionHandler,
                Func<TParameter, TResult> completeHandler
                );

            ValueTask<TResult> OneOfStatusAsync<TParameter, TResult>(
                TParameter parameter,
                ConditionActionStateComponent.StatusEnum status,
                Func<TParameter, CancellationToken, ValueTask<TResult>> noActivatedHandler,
                Func<TParameter, CancellationToken, ValueTask<TResult>> checkConditionHandler,
                Func<TParameter, CancellationToken, ValueTask<TResult>> completeHandler,
                CancellationToken cancellationToken
                );

            void SetStatus(
                ConditionActionStateComponent conditionActionState,
                ConditionActionStateComponent.StatusEnum status);
        }

        public interface ITimerSetter 
        {
            TResult OneOfStatus<TParameter, TResult>(
                TParameter parameter,
                TimerActionStateComponent.StatusEnum status,
                Func<TParameter, TResult> noActivatedHandler,
                Func<TParameter, TResult> creatingTimerHandler,
                Func<TParameter, TResult> waitingTimerHandler,
                Func<TParameter, TResult> completeHandler
                );

            ValueTask<TResult> OneOfStatusAsync<TParameter, TResult>(
                TParameter parameter,
                TimerActionStateComponent.StatusEnum status,
                Func<TParameter, CancellationToken, ValueTask<TResult>> noActivatedHandler,
                Func<TParameter, CancellationToken, ValueTask<TResult>> creatingTimerHandler,
                Func<TParameter, CancellationToken, ValueTask<TResult>> waitingTimerHandler,
                Func<TParameter, CancellationToken, ValueTask<TResult>> completeHandler,
                CancellationToken cancellationToken
                );

            void SetStatus(
                TimerActionStateComponent timerActionState,
                TimerActionStateComponent.StatusEnum status);

            void SetTimerDate(
                TimerActionStateComponent timerActionState,
                DateTimeOffset date);
        }

        #endregion
    }
}
