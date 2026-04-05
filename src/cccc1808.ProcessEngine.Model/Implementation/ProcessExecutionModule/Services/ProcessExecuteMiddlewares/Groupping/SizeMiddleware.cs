using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Groupping
{
    /// <summary>
    /// Разбивает батчи по размеру.
    /// </summary>
    public class SizeMiddleware<TId>
        : IProcessHandlerMiddleware<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Func<IServiceProvider, IProcessHandlerMiddleware<TId>> _nextFactory;
        private readonly int _chunkSize;

        public SizeMiddleware(
            IServiceProvider serviceProvider,
            Func<IServiceProvider, IProcessHandlerMiddleware<TId>> nextFactory,
            int chunkSize)
        {
            _serviceProvider = serviceProvider;
            _nextFactory = nextFactory;
            _chunkSize = chunkSize;
        }

        public async ValueTask HandleRangeAsync(
            IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>> ids,
            CancellationToken cancellationToken)
        {
            var chunkedIds = ids
                .SelectMany(e => e.Chunk(_chunkSize).ToArray())
                .ToArray();

            var handler = _nextFactory(_serviceProvider);
            await handler.HandleRangeAsync(
                chunkedIds, 
                cancellationToken);
        }
    }
}
