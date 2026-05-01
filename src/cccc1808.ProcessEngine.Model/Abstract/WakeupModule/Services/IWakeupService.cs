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
        Task<ICollection<IProcessContainer<TId>>> AfterAsyncSessionHandlerAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken);

        /// <summary>
        /// Гарантированное пробуждение процессов (извне, не из асинхронного выполнения)
        /// (берет блокировку до конца транзакции, поэтому лучше вызывать в конце транзакции).
        /// </summary>
        /// <param name="data">Id процесса.</param>
        Task WakeupProcessHandlerAsync(
            ICollection<TId> ids,
            CancellationToken cancellationToken);
    }
}