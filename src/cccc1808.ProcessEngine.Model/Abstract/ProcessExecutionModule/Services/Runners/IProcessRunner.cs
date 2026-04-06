using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners
{
    public interface IProcessRunner
         : IAsyncDisposable
    {
        Task BuildHandler();

        Task RunAsync(
            bool oneCycle, 
            CancellationToken cancellationToken);

        Task WaitRunningTasksAsync(
            CancellationToken cancellationToken
            );
    }
}
