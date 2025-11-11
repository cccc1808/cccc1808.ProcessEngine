using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation
{
    internal static class EFQueryHelper
    {
        public static async Task<Dictionary<TKey, (bool IsInserterted, TEntity Entity)>> GetOrInsertAsync<TEntity, TKey>(
            DbContext context,
            ICollection<TKey> keys,
            Func<IQueryable<TEntity>> selectQueryFunc,
            Func<TEntity, TKey> keySelectorFunc,
            Func<TKey, TEntity> buildFunc,
            CancellationToken cancellationToken)
            where TEntity : class
        {
            var founded = await selectQueryFunc()
                .ToArrayAsync(cancellationToken);

            var notFounded = keys.ToHashSet();
            foreach (var elem in founded)
            {
                notFounded.Remove(
                    keySelectorFunc(elem));
            }

            var inserted = await context.Set<TEntity>()
                .UpsertRange(notFounded.Select(e => buildFunc(e)))
                .NoUpdate()
                .RunAndReturnAsync(cancellationToken);
            var insertedKeys = inserted
                .Select(e => keySelectorFunc(e))
                .ToHashSet();

            var result = await selectQueryFunc()
                .ToArrayAsync(cancellationToken);

            return result.ToDictionary(
                e => keySelectorFunc(e),
                e => (insertedKeys.Contains(keySelectorFunc(e)), e));
        }
    }
}
