using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider
{
    public interface IQueueConsumer 
    {
        ValueTask<ICollection<MessageDto>> ConsumeBatchAsync(
            int limit,
            TimeSpan timeout,
            CancellationToken cancellationToken
            );

        /// <summary>
        /// Vожно ограничить, что мы хотим обработать (до 1000 сообщений) и (до 100 различных триггеров)
        //// т.к. хотим более строка ограничить кол-во изменений в БД на одну транзакцию, при этом коммитить больше сообщений в брокере не дорого 
        //// (Агрегация пакета сообщений в одну записб в БД).
        /// </summary>
        /// <typeparam name="TParameter"></typeparam>
        /// <param name="parameter"></param>
        /// <param name="packTimeout">Размер пака.</param>
        /// <param name="packLimit">Timeout пака.</param>
        /// <param name="batchTimeout">Timeout батча.</param>
        /// <param name="packCondition">Условие обработки пака. True - нужно получить следующий пак.</param>
        ValueTask ConsumeBatchAsync<TParameter>(
            TParameter parameter,
            TimeSpan packTimeout,
            int packLimit,
            TimeSpan batchTimeout,
            Func<TParameter, ICollection<MessageDto>, bool> packCondition,
            CancellationToken cancellationToken
            );

        ValueTask CommitAsync(
            CancellationToken cancellationToken);
    }
}
