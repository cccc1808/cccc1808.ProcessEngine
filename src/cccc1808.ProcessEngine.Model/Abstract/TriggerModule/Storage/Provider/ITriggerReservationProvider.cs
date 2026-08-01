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
        /// Пытаемся зарезервировать триггер.
        /// </summary>
        /// <returns>Процессы, которые удалось зарезервировать.</returns>
        ValueTask<ISet<TId>> TryReserveAsync(
            ICollection<TId> triggerIds,
            DateTimeOffset date,
            CancellationToken cancellationToken);

        ValueTask ContinueReserveAsync(
            ICollection<TId> triggerIds,
            DateTimeOffset date,
            CancellationToken cancellationToken);

        /// <summary>
        /// Снять резервирвоание с триггера.
        /// </summary>
        ValueTask UnreserveAsync(
            ICollection<TId> triggerIds,
            CancellationToken cancellationToken);

        /// <summary>
        /// Отчистка окружения (!! только для тестов).
        /// </summary>
        /// <returns></returns>
        ValueTask ClearAsync();
    }
}
