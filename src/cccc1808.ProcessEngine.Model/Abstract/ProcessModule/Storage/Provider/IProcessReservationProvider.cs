using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Provider
{
    /// <summary>
    /// Управление резервированием процесса.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IProcessReservationProvider<TId>
    {
        /// <summary>
        /// Инициализация состояния.
        /// </summary>
        ValueTask InitAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Пытаемся зарезервировать процессы.
        /// </summary>
        /// <returns>Процессы, которые удалось зарезервировать.</returns>
        ValueTask<ISet<TId>> TryReserveAsync(
            ICollection<TId> processIds, 
            DateTimeOffset date,
            CancellationToken cancellationToken);

        /// <summary>
        /// Снять резервирвоание с процессов.
        /// </summary>
        ValueTask UnreserveAsync(
            ICollection<TId> processIds,
            CancellationToken cancellationToken);

        /// <summary>
        /// Получить набор процессов, которын зарезервированы всеми нодами (если провайдер поддерживает).
        /// </summary>
        ValueTask<ISet<TId>> GetReservedAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Отчистка окружения (!! только для тестов).
        /// </summary>
        /// <returns></returns>
        ValueTask ClearAsync();
    }
}
