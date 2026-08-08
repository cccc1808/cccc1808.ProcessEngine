using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query
{
    public interface ITriggerSelectQuery<TId>
    {
        IContext InitContext(IOptions options, string handlerKey);

        Task<ICollection<SelectResult>> ExecuteAsync(IContext context, CancellationToken cancellationToken);


        #region types

        public interface IOptions
        {

        }

        public interface IContext 
        {
        
        }

        public readonly record struct SelectResult(
            TId TriggerId,
            string HandlerKey);

        #endregion
    }
}
