using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services
{
    public interface IProcessExecutorServices<TId>
    {
        IProcessHandlerMiddleware<TId> ResolveRootHandler(IServiceProvider serviceProvider);
    }
}
