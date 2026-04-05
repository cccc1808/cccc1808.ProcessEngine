using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Test1.Model
{
    internal class TestMiddleware
        : IProcessHandlerMiddleware<Guid>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Func<IServiceProvider, IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<Guid>>>, CancellationToken, ValueTask> _handler;

        public TestMiddleware(
            IServiceProvider serviceProvider, 
            Func<IServiceProvider, IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<Guid>>>, CancellationToken, ValueTask> handler)
        {
            _serviceProvider = serviceProvider;
            _handler = handler;
        }

        public async ValueTask HandleRangeAsync(
            IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<Guid>>> ids, 
            CancellationToken cancellationToken)
        {
            await _handler(_serviceProvider, ids, cancellationToken);
        }
    }
}
