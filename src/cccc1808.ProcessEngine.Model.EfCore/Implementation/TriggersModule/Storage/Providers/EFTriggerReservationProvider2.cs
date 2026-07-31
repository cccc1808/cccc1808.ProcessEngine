using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Providers
{
    public class EFTriggerReservationProvider2<TId>
        : ITriggerReservationProvider<TId>
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEFDbContext _dbContext;

        public EFTriggerReservationProvider2(
            IDateTimeProvider dateTimeProvider,
            IEFDbContext dbContext)
        {
            _dateTimeProvider = dateTimeProvider;
            _dbContext = dbContext;
        }

        public ValueTask<ISet<TId>> GetReservedAsync(CancellationToken cancellationToken)
        {
            // Испольуется этим провайдером.
            return ValueTask.FromResult<ISet<TId>>(
                new HashSet<TId>(0));
        }

        public ValueTask InitAsync(CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        public async ValueTask<ISet<TId>> TryReserveAsync(
            ICollection<TId> triggerIds,
            DateTimeOffset date,
            CancellationToken cancellationToken)
        {
            // Здесь уже есть update lock.
            var result = new HashSet<TId>(triggerIds.Count);
            var forQuery = new List<TriggerReserveDbEntity<TId>>(triggerIds.Count);

            foreach (var elem in triggerIds)
            {
                result.Add(elem);
                forQuery.Add(
                    new TriggerReserveDbEntity<TId>(elem, date));
            }

            await _dbContext.Set<TriggerReserveDbEntity<TId>>()
                .UpsertRange(forQuery)
                .On(e => e.Id)
                .WhenMatched((e1, e2) => new TriggerReserveDbEntity<TId>() { ReserveDate = date })
                .RunAsync(cancellationToken);

            return result;
        }

        public async ValueTask UnreserveAsync(
            ICollection<TId> triggerIds, 
            bool fromRunner, 
            CancellationToken cancellationToken)
        {
            // Отчищаем InMemory таблицу.
            await _dbContext.Set<TriggerReserveDbEntity<TId>>()
                .Where(e => triggerIds.Contains(e.Id))
                .ExecuteDeleteAsync();

            if (fromRunner)
            {
                // Для EF Обновиться через ChangeTracker.
                return;
            }

            await _dbContext.Set<ProcessDbEntity<TId>>()
                .Where(e => triggerIds.Contains(e.Id))
                .ExecuteUpdateAsync(e => e.SetProperty(e => e.ReservationTimeout, _dateTimeProvider.UtcNow));
        }

        public async ValueTask ClearAsync()
        {
            await _dbContext.Set<TriggerReserveDbEntity<TId>>()
                .ExecuteDeleteAsync();
        }
    }
}
