using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage
{
    public interface IEFDbContext
    {
        IQueryable<T> QueryFromCollection<T>(IList<T> source);

        DbSet<T> Set<T>()
            where T : class;

        Task SaveChangesAsync(CancellationToken cancellationToken);

        DbContext DbContext { get; }

        EntityEntry<T> AttachEntity<T>(
            T entity, 
            bool throwIfAttached)
            where T : class;

        void DetachEntity<T>(T entity)
            where T : class;

        void Detach(EntityEntry entry);
    }
}
