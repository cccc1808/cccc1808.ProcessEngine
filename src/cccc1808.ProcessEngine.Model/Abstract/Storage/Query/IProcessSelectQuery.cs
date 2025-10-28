using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Registry;

namespace cccc1808.ProcessEngine.Model.Abstract.Storage.Query
{
    public interface IProcessSelectQuery<TId>
    {
        IAsyncEnumerable<Queue<ProcessInstanceInfoDto<TId>>> SelectAsync(
            ContextDto context,
            ICollection<ProcessRegistryDto> types,
            CancellationToken cancellationToken);

        Task UnlockSelectAsync(
            Queue<ProcessInstanceInfoDto<TId>> ids,
            CancellationToken cancelation);

        public class ContextDto 
        {
            public int BatchSize { get; set; }
        }
    }
}
