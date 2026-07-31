using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider
{
    public interface ITriggerReservationProvider<TId>
    {
        /// <summary>
        /// Инициализация состояния.
        /// </summary>
        ValueTask InitAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Пытаемся зарезервировать триггер.
        /// </summary>
        /// <returns>Процессы, которые удалось зарезервировать.</returns>
        ValueTask<ISet<TId>> TryReserveAsync(
            ICollection<TId> triggerIds,
            DateTimeOffset date,
            CancellationToken cancellationToken);

        /// <summary>
        /// Снять резервирвоание с триггера.
        /// </summary>
        ValueTask UnreserveAsync(
            ICollection<TId> triggerIds,
            bool fromRunner,
            CancellationToken cancellationToken);

        /// <summary>
        /// Получить набор триггеров, которые зарезервированы всеми нодами (если провайдер поддерживает).
        /// </summary>
        ValueTask<ISet<TId>> GetReservedAsync(CancellationToken cancellationToken);
    }
}
