using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider
{
    public interface IQueueConsumer 
    {
        ValueTask<ICollection<MessageDto>> ConsumeBatchAsync(
            int limit,
            TimeSpan batchTimeout,
            CancellationToken cancellationToken
            );

        /// <summary>
        /// Можно ограничить, что мы хотим обработать (до 1000 сообщений) и (до 100 различных триггеров)
        //// т.к. хотим более строка ограничить кол-во изменений в БД на одну транзакцию, при этом коммитить больше сообщений в брокере не так дорого 
        //// (Агрегация пакета сообщений в одну записб в БД).
        /// </summary>
        /// <typeparam name="TParameter"></typeparam>
        /// <param name="parameter"></param>
        /// <param name="batchTimeout">Timeout батча.</param>
        /// <param name="onReceivedHandler">Хендлер сообщения. Нужно ли продолжить считывать сообщения.</param>
        ValueTask ConsumeBatchAsync<TParameter>(
            TParameter parameter,
            TimeSpan batchTimeout,
            Func<TParameter, MessageDto, bool> onReceivedHandler,
            CancellationToken cancellationToken);

        ValueTask CommitAsync(
            CancellationToken cancellationToken);
    }
}
