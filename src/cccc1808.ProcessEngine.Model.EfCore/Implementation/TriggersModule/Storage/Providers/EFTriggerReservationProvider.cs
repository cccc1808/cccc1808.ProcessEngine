using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Providers
{
    public class EFTriggerReservationProvider<TId>
        : ITriggerReservationProvider<TId>
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEFDbContext _dbContext;

        public EFTriggerReservationProvider(
            IDateTimeProvider dateTimeProvider, 
            IEFDbContext dbContext)
        {
            _dateTimeProvider = dateTimeProvider;
            _dbContext = dbContext;
        }

        public ValueTask InitAsync(CancellationToken cancellationToken)
        {
            // Не используется.
            return ValueTask.CompletedTask;
        }

        public ValueTask<ISet<TId>> GetReservedAsync(CancellationToken cancellationToken)
        {
            // Не поддерживается.
            return ValueTask.FromResult<ISet<TId>>(
                new HashSet<TId>(0));
        }

        public async ValueTask<ISet<TId>> TryReserveAsync(
            ICollection<TId> triggerIds, 
            DateTimeOffset date,
            CancellationToken cancellationToken)
        {
            var result = triggerIds.ToHashSet();

            // Здесь уже updatelock, поэтому резервируется все.
            await _dbContext.Set<TriggerDbEntity<TId>>()
                .Where(e => result.Contains(e.Id))
                .ExecuteUpdateAsync(
                    e => e.SetProperty(e => e.ReservationTimeout, date), 
                    cancellationToken);

            return result;
        }

        public async ValueTask UnreserveAsync(
            ICollection<TId> triggerIds,
            bool fromRunner,
            CancellationToken cancellationToken)
        {            
            if (fromRunner)
            {
                // Т.к. это EF и раннер, то обновиться через ChangeTracker, запрос не нужен.
                return;
            }

            // Здесь уже updatelock, поэтому резервируется все.
            await _dbContext.Set<TriggerDbEntity<TId>>()
                .Where(e => triggerIds.Contains(e.Id))
                .ExecuteUpdateAsync(
                    e => e.SetProperty(e => e.ReservationTimeout, _dateTimeProvider.UtcNow),
                    cancellationToken);
        }

        public ValueTask ClearAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
