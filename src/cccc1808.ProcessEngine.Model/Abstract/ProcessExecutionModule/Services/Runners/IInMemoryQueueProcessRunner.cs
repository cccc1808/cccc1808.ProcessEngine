using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners
{
    /// <summary>
    /// Раннер процессов, использующий inmemory очередь.
    /// Резервирует задачи для помещения в очередь.
    /// DbSelector -> InMemoryBuffer -> ConsumerAndExecutor.
    /// </summary>
    public interface IInMemoryQueueProcessRunner
        : IProcessRunner
    {
        /// <summary>
        /// In memory очередь процессов на выполнение.
        /// </summary>
        /// <typeparam name="TId"></typeparam>
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

        /// <summary>
        /// Запросы к хранилищу.
        /// </summary>
        /// <typeparam name="TId"></typeparam>
        public interface ISelectQuery<TId>
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
        }
    }
}
