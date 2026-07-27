using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.ExternalCounter
{
    /// <summary>
    /// Провайдер внешнего счетчика для триггеров.
    /// Предпологается возможность нетранакционной реализации.
    /// </summary>
    public interface IExternalCounterProvider
    {
        /// <summary>
        /// Создать счетчик.
        /// </summary>
        Task CreateCounterAsync(
            string triggerKey,
            int value,
            CancellationToken cancellationToken);

        Task RemoveCounterAsync(string triggerKey, CancellationToken cancellationToken);

        /// <summary>
        /// Проверить наличие счетчика.
        /// </summary>
        /// <param name="triggerKey"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<bool> CounterExists(
            string triggerKey,
            CancellationToken cancellationToken);

        Task<bool> CheckDecrementedAsync(string triggerKey, string processId);

        Task DecrementCompleteAsync(string triggerKey, string processId);

        Task<int> TryDecrementCounterAsync(string triggerKey, string processId);

        /// <summary>
        /// Получить данные о счетчике по триггеру.
        /// </summary>
        /// <param name="triggersKeys"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Dictionary<string, (int Counter, ISet<string> Members)>> GetCountersByTriggersAsync(
            ICollection<string> triggersKeys,
            CancellationToken cancellationToken);

        Task ClearAsync();
    }
}
