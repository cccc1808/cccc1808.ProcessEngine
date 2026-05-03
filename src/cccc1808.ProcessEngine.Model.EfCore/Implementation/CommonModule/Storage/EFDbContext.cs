using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage.Entities;

using EntityFrameworkCore.MemoryJoin;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage
{
    public class EFDbContext
        : IEFDbContext
    {
        private readonly DbContext _dbContext;

        public DbContext DbContext => _dbContext;

        public EFDbContext(DbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public DbSet<T> Set<T>() where T : class
        {
            return _dbContext.Set<T>();
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            await _dbContext.SaveChangesAsync();
        }

        public IQueryable<T> QueryFromCollection<T>(IList<T> source)
        {
            return _dbContext.FromLocalList(
                source,
                typeof(MemoryJoinStubEntity),
                ValuesInjectionMethod.ViaParameters
                );
        }

        public EntityEntry<T> AttachEntity<T>(T entity, bool throwIfAttached)
            where T : class
        {
            var set = _dbContext.Set<T>();
            var entry = set.Entry(entity);

            if (entry.State != EntityState.Detached)
            {
                if (throwIfAttached)
                {
                    throw new InvalidOperationException($"[Bug]. Сущность уже есть в ChangeTracker. {nameof(throwIfAttached)}");
                }

                return entry;
            }
            else 
            {
                return set.Attach(entity);
            }
        }

        public void DetachEntity<T>(T entity) where T : class
        {
            Detach(
                _dbContext.Set<T>().Entry(entity));
        }

        public void Detach(EntityEntry entry)
        {
            if (entry.State == EntityState.Detached)
            {
                throw new ArgumentException("[Bug]. Сущность уже отсоединина.");
            }

            entry.State = EntityState.Detached;
        }
    }
}
