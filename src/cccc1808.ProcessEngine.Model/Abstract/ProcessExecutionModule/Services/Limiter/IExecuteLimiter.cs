using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Limiter
{
    public interface IExecuteLimiterInvoker 
    {
        ValueTask WaitNextAsync(CancellationToken cancellationToken);
    }

    public interface IExecuteLimiter
    {
        ValueTask WaitNextAsync(CancellationToken cancellationToken);
    }
}
