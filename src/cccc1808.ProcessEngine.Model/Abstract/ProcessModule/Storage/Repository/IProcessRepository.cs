using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository
{
    public interface IProcessRepository<TId>
    {
        //Task<ICollection<IProcessContainer<TId>>> GetRange(
        //    ICollection<TId> ids,
        //    bool withLock,
        //    CancellationToken cancellationToken);

        /// <summary>
        /// For update skip locked, where <see cref="IProcessEntity_AsyncExecute_Condition"/>
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<ICollection<IProcessContainer<TId>>> GetForAsyncProcessingRangeAsync(
            ICollection<ProcessInstanceInfoDto<TId>> ids,
            CancellationToken cancellationToken);

        Task<ICollection<IProcessContainer<TId>>> GetWaitingRangeAsync(
            ICollection<TId> ids,
            bool updateLock,
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
    }
}
