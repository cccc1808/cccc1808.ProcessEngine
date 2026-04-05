using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Query
{
    public interface IProcessAsyncProcessingSelectQuery<TId>
    {
        /// <summary>
        /// Отобор идентификаторов процессов для обработки.
        /// Задает <see cref="IProcess"/>
        /// </summary>
        /// <param name="batchSize"></param>
        /// <param name="types"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        IAsyncEnumerable<Queue<ProcessInstanceInfoDto<TId>>> SelectProcessIdsForAsyncProcessingAsync(
            LinkContainer<(object? _, int BatchSize)> context,
            ICollection<ProcessRegistryDto> types,
            CancellationToken cancellationToken);

        Task UnlockSelectAsync(
            Queue<ProcessInstanceInfoDto<TId>> ids,
            CancellationToken cancelation);
    }
}
