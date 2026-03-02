using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Entities.Conditions
{
    /// <summary>
    /// Получение связанной сущности по ключу.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    /// <typeparam name="TEntity"></typeparam>
    public class ProcessLinkedDbEntity_RangeCondition<TId, TEntity> :         
        IInMemoryCondition<TEntity, TId>,
        IQueryableCondition<TEntity, ICollection<TId>>
        where TEntity : IProcessLinkedDbEntity<TId>
    {
        public bool Check(TEntity source, TId parameters)
        {
            return Comparer<TId>.Default.Compare(source.ProcessId, parameters) == 0;
        }

        public IEnumerable<TEntity> ApplayEnumerable(IEnumerable<TEntity> source, TId parameters)
        {
            return source.Where(e => Check(e, parameters));
        }

        public IQueryable<TEntity> ApplayQueryable(
            IQueryable<TEntity> source,
            ICollection<TId> parameters)
        {
            return source.Where(e => parameters.Contains(e.ProcessId));
        }        
    }
}
