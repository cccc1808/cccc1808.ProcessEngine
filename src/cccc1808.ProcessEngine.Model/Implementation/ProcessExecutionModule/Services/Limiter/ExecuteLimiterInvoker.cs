using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Limiter;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Limiter
{
    public class ExecuteLimiterInvoker : IExecuteLimiterInvoker
    {
        private readonly IExecuteLimiter[] _limiters;

        public ExecuteLimiterInvoker(
            IEnumerable<IExecuteLimiter> limiters)
        {
            _limiters = limiters.ToArray();
        }

        public async ValueTask WaitNextAsync(CancellationToken cancellationToken)
        {
            foreach (var elem in _limiters)
            {
                await elem.WaitNextAsync(cancellationToken);
            }
        }
    }
}
