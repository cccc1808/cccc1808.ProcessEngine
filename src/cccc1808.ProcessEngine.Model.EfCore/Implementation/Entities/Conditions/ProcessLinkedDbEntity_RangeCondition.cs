using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Entities.Conditions
{
    public class ProcessLinkedDbEntity_RangeCondition<TId, TEntity>
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
