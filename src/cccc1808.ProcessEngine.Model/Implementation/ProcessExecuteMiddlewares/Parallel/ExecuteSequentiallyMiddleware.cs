using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Services.ProcessExecuteMiddlewares;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Parallel
{
    /// <summary>
    /// Выполняет батчи последовательно.
    /// </summary>
    public class ExecuteSequentiallyMiddleware<TId>
        : IProcessHandlerMiddleware<TId>
    {
        private readonly Func<
            IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>>, 
            CancellationToken, 
            ValueTask<IProcessHandlerMiddleware<TId>>>  _handlerFactory;

        public ExecuteSequentiallyMiddleware(
            Func<IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>>, 
                CancellationToken, 
                ValueTask<IProcessHandlerMiddleware<TId>>> handlerFactory)
        {
            _handlerFactory = handlerFactory;
        }

        public async ValueTask HandleRangeAsync(
            IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>> ids,
            CancellationToken cancellationToken)
        {
            var handler = await _handlerFactory(ids, cancellationToken);

            foreach (var elem in ids)
            {
                await handler.HandleRangeAsync(
                    [elem],
                    cancellationToken
                    );
            }
        }
    }
}
