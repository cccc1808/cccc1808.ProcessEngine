using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Storage
{
    public interface IEFDbContext
    {
        IQueryable<T> QueryFromCollection<T>(IList<T> source);

        DbSet<T> Set<T>()
            where T : class;

        Task SaveChangesAsync(CancellationToken cancellationToken);

        DbContext DbContext { get; }
    }
}
