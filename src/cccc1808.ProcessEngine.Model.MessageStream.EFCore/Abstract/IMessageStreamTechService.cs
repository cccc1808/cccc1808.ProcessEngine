using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;

namespace cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Abstract
{
    /// <summary>
    /// Технические сервисы, необходимые для работы стрима.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IMessageStreamTechService<TId>
    {
        /// <summary>
        /// Хендлер асинхронной обработки стрима, необходимо вызывать до и после ассинхронной обработки пакета сообщений.
        /// </summary>
        /// <param name="streams"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task BeforeStreamExecuteAsync(
            ICollection<IProcessContainer<TId>> streams,
            CancellationToken cancellationToken);

        /// <summary>
        /// Хендлер асинхронной обработки стрима, необходимо вызывать до и после ассинхронной обработки пакета сообщений.
        /// </summary>
        /// <param name="streams"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task AfterStreamExecuteAsync(
            ICollection<IProcessContainer<TId>> streams, 
            CancellationToken cancellationToken);

        /// <summary>
        /// Необходимо вызывать в конце транзакции, в которой в stream публиковались сообщения.
        /// Пробуждает стрим, если он спит.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="cancellationToken"></param>
        Task WakeUpStreamAfterMessageInsertedIfNeedAsync(
            (TId StreamId, DateTimeOffset? delayMinDate)[] data, 
            CancellationToken cancellationToken);
    }
}