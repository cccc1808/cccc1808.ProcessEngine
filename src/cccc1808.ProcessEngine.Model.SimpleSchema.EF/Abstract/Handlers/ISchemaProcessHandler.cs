using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers
{
    public interface ISchemaProcessHandler<TId>
    {
        bool CanExecuteServiceTask(string name);

        ValueTask<bool> ExecuteServiceTask(
            string name,
            string actionId,
            IProcessContainer<TId> process, 
            CancellationToken cancellationToken);

        bool CanCheckCondition(string name);

        ValueTask<bool> CheckConditionAsync(
            string name,
            string actionId,
            IProcessContainer<TId> process,
            CancellationToken cancellationToken);

        bool CanExecuteConditionHandler(string name);

        ValueTask<bool> ExecuteConditionHandlerAsync(
            string name,
            string actionId,
            IProcessContainer<TId> process,
            CancellationToken cancellationToken);

        bool CanExecuteTimer(string name);

        ValueTask<bool> ExecuteTimerAsync(
            string name,
            string actionId,
            IProcessContainer<TId> process,
            CancellationToken cancellationToken);
    }
}
