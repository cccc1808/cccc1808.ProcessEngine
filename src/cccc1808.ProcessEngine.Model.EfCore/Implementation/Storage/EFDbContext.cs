using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Storage;

using EntityFrameworkCore.MemoryJoin;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Storage
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
    }
}
