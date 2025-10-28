using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.Services.ProcessExecuteMiddlewares
{
    public interface IProcessHandlerMiddleware<TId>
    {
        ValueTask HandleRangeAsync(
            IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>> ids,
            CancellationToken cancellationToken);
    }
}
