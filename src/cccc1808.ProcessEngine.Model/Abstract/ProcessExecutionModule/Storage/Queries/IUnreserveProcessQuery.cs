using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Storage.Queries
{
    /// <summary>
    /// Снятие резервирование процесса на выполнение (другие параллельные раннеры могут брать в обработку).
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IUnreserveProcessQuery<TId>
    {
        Task UnreserveAsync(
            Queue<ProcessInstanceInfoDto<TId>> ids,
            CancellationToken cancelation);

        Task UnreserveAsync(
            ICollection<TId> ids,
            CancellationToken cancelation);
    }
}
