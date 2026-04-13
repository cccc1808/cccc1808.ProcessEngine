using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Handlers
{
    /// <summary>
    /// Хендлер проверки условия <see cref="ProcessStatusEnum.AsyncExecute"/> / <see cref="ProcessStatusEnum.WaitEvent"/> 
    /// после того, как получена блокировка над wakeup компонентом.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IWakeupCheckHandler<TId>
    {
        /// <summary>
        /// Проверка условия в конце сессии асинхронной обработки.
        /// </summary>
        /// <param name="processes">Список процессов для проверки.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True - процесс должен остаться в <see cref="ProcessStatusEnum.AsyncExecute", False - процесс может перейти в <see cref="ProcessStatusEnum.WaitEvent"</returns>
        ValueTask<IDictionary<TId, bool>> HandleRangeAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken);
    }
}
