using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services
{
    /// <summary>
    /// Оборачивает <see cref="IExternalCounterProvider"/>, регистрирует действия компенсации.
    /// </summary>
    public interface IExternalCounterContext
    {
        /// <summary>
        /// <see cref="IExternalCounterProvider.CreateCounterAsync(string, int, CancellationToken)"/>
        /// В случае ошибки будет попытка удалить счетчик.
        /// </summary>
        /// <param name="triggerKey"></param>
        /// <param name="value"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task CreateCounterAsync(string triggerKey, int value, CancellationToken cancellationToken);

        /// <summary>
        /// <see cref="IExternalCounterProvider.TryDecrementCounterAsync(string, string)"/>.
        /// В случае ошибки будет попытка увеличить счетчик и снять отметку (вернуть в исходное состояние).
        /// В случае коммита транзакции будет попытка снять отметку.
        /// </summary>
        /// <param name="triggerKey"></param>
        /// <param name="processIdString"></param>
        /// <returns></returns>
        Task<int> TryDecrementCounterAsync(string triggerKey, string processIdString);
    }
}