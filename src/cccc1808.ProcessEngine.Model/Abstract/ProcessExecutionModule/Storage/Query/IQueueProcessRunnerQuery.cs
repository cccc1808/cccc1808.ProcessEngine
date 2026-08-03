using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Storage.Query
{
    public interface IQueueProcessRunnerQuery<TId>
    {
        IContext InitContext(IOptions options, ProcessRegistryDto processType);

        Task<ICollection<SelectResult>> ExecuteAsync(IContext context, CancellationToken cancellationToken);


        #region types

        public interface IOptions
        {

        }

        public interface IContext
        {

        }

        public readonly record struct SelectResult(
            TId ProcessId,
            ProcessTypeDto ProcessType,
            short Priority);

        #endregion
    }
}
