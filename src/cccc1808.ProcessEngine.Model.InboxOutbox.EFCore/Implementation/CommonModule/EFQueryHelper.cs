using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.CommonModule
{
    internal static class EFQueryHelper
    {
        public static async Task<Dictionary<TKey, (bool IsInserterted, TEntity Entity)>> GetOrInsertAsync<TEntity, TKey>(
            IEFDbContext context,
            ICollection<TKey> keys,
            Func<ICollection<TKey>, IQueryable<TEntity>, IQueryable<TEntity>> selectQueryFunc,
            Func<TEntity, TKey> keySelectorFunc,
            Expression<Func<TEntity, object>> unique,
            Func<ICollection<TKey>, CancellationToken, ValueTask<ICollection<TEntity>>> buildFunc,
            CancellationToken cancellationToken)
            where TEntity : class
        {
            var founded = await selectQueryFunc(
                keys,
                context.Set<TEntity>().AsNoTracking())
                .AsNoTracking()
                .ToArrayAsync(cancellationToken);

            var notFounded = keys.ToHashSet();
            foreach (var elem in founded)
            {
                notFounded.Remove(
                    keySelectorFunc(elem));
            }

            var newEntities = await buildFunc(notFounded, cancellationToken);
            var inserted = await context.Set<TEntity>()
                .UpsertRange(newEntities)
                .On(unique)
                .NoUpdate()
                .RunAndReturnAsync(cancellationToken);

            var insertedKeys = inserted
                .Select(e => keySelectorFunc(e))
                .ToHashSet();

            var result = await selectQueryFunc(
                keys,
                context.Set<TEntity>().AsNoTracking()
                )                
                .ToArrayAsync(cancellationToken);

            return result.ToDictionary(
                e => keySelectorFunc(e),
                e => (insertedKeys.Contains(keySelectorFunc(e)), e));
        }
    }
}
