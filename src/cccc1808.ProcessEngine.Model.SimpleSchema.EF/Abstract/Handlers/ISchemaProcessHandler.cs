using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers
{
    public interface ISchemaProcessHandler
    {
        public readonly record struct ExecuteServiceTaskResult(
            bool IsComplete,
            ActivateActionDto[] ActivateActions)
        {
            public static ExecuteServiceTaskResult Result(bool isComplete, params ActivateActionDto[] activateActions)
                => new ExecuteServiceTaskResult(isComplete, activateActions);
        }

        public readonly record struct ExecuteConditionResult(
            ActivateActionDto[] ActivateActions)
        {
            public static ExecuteConditionResult Result(params ActivateActionDto[] activateActions)
                => new ExecuteConditionResult(activateActions);
        }

        public readonly record struct ExecuteTimerResult(
            ActivateActionDto[] ActivateActions)
        {
            public static ExecuteTimerResult Result(params ActivateActionDto[] activateActions)
                => new ExecuteTimerResult(activateActions);
        }

        /// <summary>
        /// Указывает на необходимость активировать действие.
        /// </summary>
        /// <param name="ActionId">Идентефикатор действия.</param>
        /// <param name="AsyncExecuteOrWaitSignal">
        /// True - асинхронное выполнение нужно сейчас,
        /// False - асинхронное выполнение не нужно, ожидается внешний сигнал или воздействие.
        /// </param>
        public readonly record struct ActivateActionDto(
            string ActionId,
            bool AsyncExecuteOrWaitSignal)
        {
            /// <summary>
            /// <see cref="ServiceTaskTokenAction"/>.
            /// Если активируем, то всегда нужно асинхронное выполнение.
            /// Иначе - true, и так выполняется.
            /// </summary>
            public static ActivateActionDto ServiceTask(string actionId)
                => new ActivateActionDto(actionId, AsyncExecuteOrWaitSignal: true);

            /// <summary>
            /// <see cref="TimerTokenAction"/>.
            /// Если активируем, то нужно создать таймер триггер и будет асинхронное выполнение.
            /// Иначе - false, ожидает срабатывания таймера.
            /// </summary>
            /// <param name="actionId"></param>
            /// <returns></returns>
            public static ActivateActionDto TimerAction(string actionId)
                => new ActivateActionDto(actionId, AsyncExecuteOrWaitSignal: true);

            /// <summary>
            /// <see cref="ConditionTokenAction"/>.
            /// </summary>
            /// <param name="actionId">Идентефикатор действия.</param>
            /// <param name="asyncExecuteOrWaitSignal">
            /// True - условие можно проверить прямо сейчас.
            /// False - улосвие нужно будет првоерять только после внешнего сигнала или воздействия.
            /// </param>
            /// <returns></returns>
            public static ActivateActionDto ConditionAction(
                string actionId,
                bool asyncExecuteOrWaitSignal)
                => new ActivateActionDto(actionId, asyncExecuteOrWaitSignal);
        }
    }

    public interface ISchemaProcessHandler<TId> : ISchemaProcessHandler
    {
        bool CanExecuteServiceTask(string name);

        ValueTask<ExecuteServiceTaskResult> ExecuteServiceTask(
            ExecuteParametersDto parameters,
            CancellationToken cancellationToken);

        bool CanCheckCondition(string name);

        ValueTask<bool> CheckConditionAsync(
            ExecuteParametersDto parameters,
            CancellationToken cancellationToken);

        bool CanExecuteConditionHandler(string name);

        ValueTask<ExecuteConditionResult> ExecuteConditionHandlerAsync(
            ExecuteParametersDto parameters,
            CancellationToken cancellationToken);

        bool CanExecuteTimer(string name);

        ValueTask<ExecuteTimerResult> ExecuteTimerAsync(
            ExecuteParametersDto parameters,
            CancellationToken cancellationToken);

        public readonly record struct ExecuteParametersDto(
            string handlerName,
            string actionId,
            IProcessContainer<TId> process,
            ISchemaProcessComponent schemaComponent);        
    }
}
