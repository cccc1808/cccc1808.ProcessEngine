using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Parallel
{
    /// <summary>
    /// Выполняет батчи параллельно.
    /// </summary>
    public class ExecuteParallelMiddleware<TId>
        : IProcessHandlerMiddleware<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Func<
            IServiceProvider, 
            IReadOnlyList<ProcessInstanceInfoDto<TId>>,
            IProcessHandlerMiddleware<TId>
            > _nextFactory;
        Func<IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>>, int?> _degreeOfParallelism;

        public ExecuteParallelMiddleware(
            IServiceProvider serviceProvider,
            Func<
                IServiceProvider,
                IReadOnlyList<ProcessInstanceInfoDto<TId>>,
                IProcessHandlerMiddleware<TId>> nextFactory,
            Func<IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>>, int?> degreeOfParallelism)
        {
            _serviceProvider = serviceProvider;
            _nextFactory = nextFactory;
            _degreeOfParallelism = degreeOfParallelism;
        }

        public async ValueTask HandleRangeAsync(
            IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>> ids,
            CancellationToken cancellationToken)
        {
            var parallelism = _degreeOfParallelism(ids) ?? -1;

            await System.Threading.Tasks.Parallel.ForAsync(
                0,
                ids.Count,
                new ParallelOptions() 
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = parallelism
                },
                async (i, t) => 
                {
                    await using var scope = _serviceProvider.CreateAsyncScope();
                    var handler = _nextFactory(scope.ServiceProvider, ids[i]);
                    await handler.HandleRangeAsync(
                        [ids[i]], cancellationToken
                        );
                }
                );
        }
    }
}
