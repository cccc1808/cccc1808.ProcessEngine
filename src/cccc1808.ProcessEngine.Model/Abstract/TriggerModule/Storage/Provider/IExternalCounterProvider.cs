using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider
{
    /// <summary>
    /// Провайдер внешнего счетчика для триггеров.
    /// Предпологается возможность нетранзакционной реализации (InMemory store).
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

        /// <summary>
        /// Удалить счетчик.
        /// </summary>
        Task RemoveCounterAsync(string triggerKey, CancellationToken cancellationToken);

        /// <summary>
        /// Проверить наличие счетчика по триггеру.
        /// </summary>
        /// <param name="triggerKey"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<bool> CounterExists(
            string triggerKey,
            CancellationToken cancellationToken);

        /// <summary>
        /// Если процес уменьшение счетчика процессом уже было, то выполняет компенсацию.
        /// Удаляет из MemberSet и увеличивает счетчик.
        /// </summary>
        /// <returns>Была ли выполнена компенсация.</returns>
        Task<bool> CompensateCounterAsync(string triggerKey, string processId);

        /// <summary>
        /// Уменьшает значение счетчика по триггеру (до коммита транзакции БД).
        /// Проверяя уникальность процесса по ProcessId (MemberSet).
        /// </summary>
        /// <returns>Значение счетчика.</returns>
        Task<int> TryDecrementCounterAsync(string triggerKey, string processId);

        /// <summary>
        /// Подтвердает уменьшение счетчика (после завершения транзакции БД).
        /// Удаляет из memberSet.
        /// </summary>
        /// <param name="triggerKey"></param>
        /// <param name="processId"></param>
        /// <returns></returns>
        Task CommitCounterAsync(string triggerKey, string processId);        

        /// <summary>
        /// Получить данные о счетчике по триггеру.
        /// Может использоваться как всполомагательные данные для хендлера триггера.
        /// </summary>
        Task<Dictionary<string, (int Counter, ISet<string> Members)>> GetCountersByTriggersAsync(
            ICollection<string> triggersKeys,
            CancellationToken cancellationToken);

        /// <summary>
        /// Отчиска окружения.
        /// Удалить все ключи счетчиков (для тестов). 
        /// </summary>
        Task ClearAsync();
    }
}
