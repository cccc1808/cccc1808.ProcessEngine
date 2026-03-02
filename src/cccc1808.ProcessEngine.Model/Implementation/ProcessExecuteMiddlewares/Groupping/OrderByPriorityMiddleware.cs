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
    /// Сортирует группы по приоритету.
    /// </summary>
    public class OrderByPriorityMiddleware<TId>
        : IProcessHandlerMiddleware<TId>
    {
        private readonly IProcessHandlerMiddleware<TId> _next;

        public OrderByPriorityMiddleware(IProcessHandlerMiddleware<TId> next)
        {
            _next = next;
        }

        public async ValueTask HandleRangeAsync(
            IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>> ids,
            CancellationToken cancellationToken)
        {
            var orderedIds = ids.Select(
                e => e.Select(e2 => e2)
                .OrderByDescending(e => e.Priority)
                .ToArray())
                .ToArray();

            await _next.HandleRangeAsync(orderedIds, cancellationToken);
        }
    }
}
