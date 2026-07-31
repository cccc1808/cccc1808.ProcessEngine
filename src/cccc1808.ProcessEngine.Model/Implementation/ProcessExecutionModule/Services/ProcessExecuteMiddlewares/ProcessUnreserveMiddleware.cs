using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Provider;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares
{
    /// <summary>
    /// Middleware для снятия резервирования процесса.
    /// Необходимо для (RedisProcessingRegistry).
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class ProcessUnreserveMiddleware<TId> 
        : IProcessHandlerMiddleware<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IProcessReservationProvider<TId> _processReservationProvider;

        private readonly Func<IServiceProvider, IProcessHandlerMiddleware<TId>> _nextFactory;

        public ProcessUnreserveMiddleware(
            IServiceProvider serviceProvider,
            IProcessReservationProvider<TId> processReservationProvider,
            Func<IServiceProvider, IProcessHandlerMiddleware<TId>> nextFactory
            )
        {
            _serviceProvider = serviceProvider;
            _processReservationProvider = processReservationProvider;
            _nextFactory = nextFactory;
        }

        public async ValueTask HandleRangeAsync(
            IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>> ids, 
            CancellationToken cancellationToken)
        {
            var next = _nextFactory(_serviceProvider);
            await next.HandleRangeAsync(ids, cancellationToken);

            // TODO: Если для БД, то прикинуть skip locked.
            await _processReservationProvider.UnreserveAsync(
                ids.SelectMany(e => e)
                    .Select(e => e.Id)
                    .ToArray(),
                cancellationToken);
        }
    }
}
