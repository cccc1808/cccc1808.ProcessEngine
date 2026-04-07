using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage
{
    public interface IProcessDbProvider<TId>
    {
        /// <summary>
        /// Загрузить процессы.
        /// </summary>
        /// <param name="processes"></param>
        /// <param name="withLock"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task LoadRangeAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            bool withLock,
            CancellationToken cancellationToken);

        /// <summary>
        /// Необязательный функционал для асинхронной обработки,
        /// позволяющий использовать свой запрос для загрузки процессов (а не только данных процесса).
        /// * В реплизации необходимо добавить значения в loadBuffer и удалить значения из notLoadedProcesses.
        /// * В реализации не забывать сбросить SelectLockTimeout.
        /// * Ориентировано на процессы стримы (с сообщениями)
        /// <see cref="cccc1808.ProcessEngine.Model.EfCore.Abstract.MessageStreamModule.Entities.IMessageDbEntity<TId>"/>.
        /// </summary>
        /// <param name="notLoadedProcesses"></param>
        /// <param name="loadBuffer"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task LoadProcessForAsyncProcessingAsync(
            IDictionary<TId, ProcessInstanceInfoDto<TId>> notLoadedProcesses,            
            IDictionary<TId, IProcessContainer<TId>> loadBuffer,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken);

        /// <summary>
        /// Загрузить данные процесса.
        /// </summary>
        Task LoadProcessDataAsync(            
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken  cancellationToken);

        /// <summary>
        /// Обновить данные процесса в хранилище.
        /// </summary>
        Task UpdateAsync(
            ICollection<IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken
            );
    }

}
