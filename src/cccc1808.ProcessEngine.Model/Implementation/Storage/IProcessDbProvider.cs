using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.Storage
{
    public interface IProcessDbProvider<TId>
    {
        /// <summary>
        /// Загрузить данные процесса для асинхронной обработки.
        /// </summary>
        Task LoadForAsyncProcessingAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,               
            CancellationToken  cancellationToken
            );

        /// <summary>
        /// Обновить данные процесса в хранилище.
        /// </summary>
        Task UpdateAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken
            );
    }
}
