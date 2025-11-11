using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Condition;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites.Conditions
{
    public class IProcessLinkedDbEntity_RangeCondition<TId, TEntity>
        : IQueryableCondition<TEntity, ICollection<TId>>
        where TEntity : IProcessLinkedDbEntity<TId>
    {
        public IQueryable<TEntity> ApplayQueryable(
            IQueryable<TEntity> source,
            ICollection<TId> parameters)
        {
            return source.Where(e => parameters.Contains(e.ProcessId));
        }
    }
}
