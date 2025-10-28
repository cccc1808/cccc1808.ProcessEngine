using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Services.ProcessExecuteMiddlewares;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares
{
    public class SelectHandlerMiddleware<TId>
        : IProcessHandlerMiddleware<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Func<IServiceProvider, ProcessInstanceInfoDto<TId>, IProcessHandlerMiddleware<TId>> _factory;

        public SelectHandlerMiddleware(
            IServiceProvider serviceProvider,
            Func<IServiceProvider, ProcessInstanceInfoDto<TId>, IProcessHandlerMiddleware<TId>> factory)
        {
            _serviceProvider = serviceProvider;
            _factory = factory;
        }

        public async ValueTask HandleRangeAsync(
            IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>> ids,
            CancellationToken cancellationToken)
        {
            if (ids.Count != 1)
            {
                throw new ArgumentException("");
            }

            var handler = _factory(_serviceProvider, ids.First().First());
            await handler.HandleRangeAsync(ids, cancellationToken);
        }
    }
}
