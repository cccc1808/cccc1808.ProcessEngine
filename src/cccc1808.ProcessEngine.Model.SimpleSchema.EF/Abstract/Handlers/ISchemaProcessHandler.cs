using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers
{
    public interface ISchemaProcessHandler
    {
        public readonly record struct ExecuteServiceTaskResult(
            bool IsComplete,
            string[] ActivateActions);

        public readonly record struct ExecuteConditionResult(
            string[] ActivateActions);

        public readonly record struct ExecuteTimerResult(
            string[] ActivateActions);
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
