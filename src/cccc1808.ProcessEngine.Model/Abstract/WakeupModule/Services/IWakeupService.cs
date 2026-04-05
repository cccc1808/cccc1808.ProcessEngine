using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services
{
    /// <summary>
    /// Реализация системы гарантированного внешнего пробуждения процесса.
    /// Используется для гарантированного пробуджения.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IWakeupService<TId>
    {
        /// <summary>
        /// Хендлер, который необходимо вызвать в конце сессии асинхронной обработки.
        /// Логика проверки пробуждения (наличие сигнала пробуждения).
        /// </summary>
        Task AfterAsyncSessionHandlerAsync(
            ICollection<IProcessContainer<TId>> processes,
            Func<ICollection<IProcessContainer<TId>>, CancellationToken, ValueTask> saveHandler, 
            CancellationToken cancellationToken);

        /// <summary>
        /// Гарантированное пробуждение процессов 
        /// (берет блокировку до конца транзакции, поэтому лучше вызывать в конце транзакции).
        /// </summary>
        /// <param name="data">Id процесса и дата таймера.</param>
        Task WakeupProcessHandlerAsync(
            TId[] ids,
            CancellationToken cancellationToken);
    }
}