
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Services
{
    /// <summary>
    /// Реализация системы гарантированного внешнего пробуждения процесса.
    /// Используется для конкурентного пробуждения.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IWakeUpService<TId>
    {
        /// <summary>
        /// Хендлер, который необходимо вызвать в конце сессии асинхронной обработки.
        /// Логика проверки пробуждения (наличие сигнала пробуждения).
        /// </summary>
        Task AfterAsyncSessionHandlerAsync(
            ICollection<IProcessContainer<TId>> processes,
            // Func<ICollection<IProcessContainer<TId>>, CancellationToken, ValueTask<ICollection<(TId, bool)>>> checkWakeUp, 
            Func<ICollection<IProcessContainer<TId>>, CancellationToken, ValueTask> saveHandler, 
            CancellationToken cancellationToken);

        /// <summary>
        /// Логика, которую нужно вызывать в конце транзакции.
        /// Пробуждает указанные процессы.
        /// </summary>
        /// <param name="data">Id процесса и дата таймера.</param>
        Task WakeUpProcessHandlerAsync(
            (TId Id, DateTimeOffset? delayMinDate)[] data, 
            CancellationToken cancellationToken);
    }
}