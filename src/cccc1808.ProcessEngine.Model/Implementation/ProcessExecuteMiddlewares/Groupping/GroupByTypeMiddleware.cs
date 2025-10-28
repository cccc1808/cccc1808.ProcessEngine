using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Services.ProcessExecuteMiddlewares;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Groupping
{
    /// <summary>
    /// Разбивает на чанки по типу процесса.
    /// </summary>
    public class GroupByTypeMiddleware<TId>
        : IProcessHandlerMiddleware<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Func<
            IServiceProvider,
            IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>>,
            CancellationToken,
            ValueTask<IProcessHandlerMiddleware<TId>>
            > _nextFactory;

        public GroupByTypeMiddleware(
            IServiceProvider serviceProvider,
            Func<
                IServiceProvider, 
                IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>>,
                CancellationToken, 
                ValueTask<IProcessHandlerMiddleware<TId>>> nextFactory)
        {
            _serviceProvider = serviceProvider;
            _nextFactory = nextFactory;
        }

        public async ValueTask HandleRangeAsync(
            IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>> ids,
            CancellationToken cancellationToken)
        {
            if (ids.Count == 0) 
            {
                return;
            }
            else if (ids.Count != 1)
            {
                throw new ArgumentException("Допускается один батч.");
            }

            var groupedByProcessTypeIds = ids.First()
                .GroupBy(e => e.ProcessType)
                .Select(e => e.Select(e2 => e2).ToArray())
                .ToArray();

            var handler = await _nextFactory(
                _serviceProvider,
                groupedByProcessTypeIds,
                cancellationToken);

            await handler.HandleRangeAsync(
                groupedByProcessTypeIds,
                cancellationToken);           
        }
    }
}
