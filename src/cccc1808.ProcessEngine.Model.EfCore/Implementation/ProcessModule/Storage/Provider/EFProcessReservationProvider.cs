using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Provider
{
    public class EFProcessReservationProvider<TId, TEntity>
        : IProcessReservationProvider<TId>
        where TEntity : ProcessDbEntity<TId>
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEFDbContext _dbContext;

        public EFProcessReservationProvider(
            IDateTimeProvider dateTimeProvider,
            IEFDbContext dbContext)
        {
            _dateTimeProvider = dateTimeProvider;
            _dbContext = dbContext;
        }

        public ValueTask ClearAsync()
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask<ISet<TId>> GetReservedAsync(CancellationToken cancellationToken)
        {
            // Данный провайдер не использует и не отслежиавет зарезервированные ноды.
            return ValueTask.FromResult<ISet<TId>>(
                new HashSet<TId>(0));
        }

        public ValueTask InitAsync(CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        public async ValueTask<ISet<TId>> TryReserveAsync(
            ICollection<TId> processIds,
            DateTimeOffset date, 
            CancellationToken cancellationToken)
        {
            var ids = processIds.ToHashSet();

            // В этой реализации значит, что получили блокировку.
            // Тут обновляем дату резервирвоания.
            await _dbContext.Set<TEntity>()
                .Where(e => ids.Contains(e.Id))
                .ExecuteUpdateAsync(
                    e => e.SetProperty(e => e.ReservationTimeout, date),
                    cancellationToken);

            return ids;
        }

        public async ValueTask UnreserveAsync(
            ICollection<TId> processIds, 
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;

            // Снимаем блокировку выборки.
            await _dbContext.Set<TEntity>()
                .Where(e => processIds.Contains(e.Id))
                .ExecuteUpdateAsync(
                    e => e.SetProperty(e => e.ReservationTimeout, now),
                    cancellationToken);
        }
    }
}
