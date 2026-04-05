using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners
{
    public interface ILocalProcessBufferService<TId>
    {
        int FreeSpace { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>FreeSpace</returns>
        (int FreeSpace, Queue<ProcessInstanceInfoDto<TId>> ids) TryProduce(
            Queue<ProcessInstanceInfoDto<TId>> ids);

        /// <summary>
        /// Получить батчи задач из очереди.
        /// </summary>
        /// <param name="limit">Лимит размера батча.</param>
        /// <param name="timeout">Лимит времени считывания батча. (Timeout с начала считывания)</param>
        [Obsolete($"Для раннера рекомендуется {nameof(ConsumeBatch2Async)}")]
        ValueTask<IReadOnlyList<ProcessInstanceInfoDto<TId>>> ConsumeBatchAsync(
            int limit,
            TimeSpan timeout,
            CancellationToken cancellationToken);

        /// <summary>
        /// Получить батчи задач из очереди.
        /// </summary>
        /// <param name="limit">Лимит размера батча.</param>
        /// <param name="timeout">Лимит времени считывания батча. (Timeout считается с момента считывания первого элемента)</param>
        ValueTask<IReadOnlyList<ProcessInstanceInfoDto<TId>>> ConsumeBatch2Async(
            int limit,
            TimeSpan timeout,
            CancellationToken cancellationToken);

        IDisposable AddEmptyHandler(
            Action<ILocalProcessBufferService<TId>> handler);
    }
}
