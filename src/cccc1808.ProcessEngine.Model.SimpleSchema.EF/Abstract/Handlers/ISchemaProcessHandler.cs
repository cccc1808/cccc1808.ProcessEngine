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
    public interface ISchemaProcessHandler<TId>
    {
        bool CanExecuteServiceTask(string name);

        ValueTask<bool> ExecuteServiceTask(
            ExecuteParametersDto parameters,
            CancellationToken cancellationToken);

        bool CanCheckCondition(string name);

        ValueTask<bool> CheckConditionAsync(
            ExecuteParametersDto parameters,
            CancellationToken cancellationToken);

        bool CanExecuteConditionHandler(string name);

        ValueTask ExecuteConditionHandlerAsync(
            ExecuteParametersDto parameters,
            CancellationToken cancellationToken);

        bool CanExecuteTimer(string name);

        ValueTask<bool> ExecuteTimerAsync(
            ExecuteParametersDto parameters,
            CancellationToken cancellationToken);

        public readonly record struct ExecuteParametersDto(
            string handlerName,
            string actionId,
            IProcessContainer<TId> process,
            ISchemaProcessComponent schemaComponent);
    }
}
