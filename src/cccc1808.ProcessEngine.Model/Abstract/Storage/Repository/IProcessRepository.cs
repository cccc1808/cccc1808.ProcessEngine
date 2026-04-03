using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.Storage.Repository
{
    public interface IProcessRepository<TId>
    {
        Task<ICollection<IProcessContainer<TId>>> GetRange(
            ICollection<ProcessIdDto<TId>> ids,
            bool withLock,
            CancellationToken cancellationToken);

        /// <summary>
        /// For update skip locked, where <see cref="IProcessEntity_AsyncExecute_Condition"/>
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<ICollection<IProcessContainer<TId>>> GetRangeForAsyncProcessingAsync(
            ICollection<ProcessIdDto<TId>> ids,
            CancellationToken cancellationToken);

        /// <summary>
        /// Обновить данные процесса.
        /// </summary>
        /// <param name="processes"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task UpdateAsync(
            ICollection<IProcessContainer<TId>> processes, 
            CancellationToken cancellationToken);

        /// <summary>
        /// Обновить данные модул
        /// </summary>
        /// <param name="processes"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task UpdateWakeupAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken);
    }
}
