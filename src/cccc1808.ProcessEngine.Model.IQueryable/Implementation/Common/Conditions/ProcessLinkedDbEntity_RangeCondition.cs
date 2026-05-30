using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Model.IQueryable.Implementation.Common.Conditions
{
    /// <summary>
    /// Получение связанной сущности по ключу.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    /// <typeparam name="TEntity"></typeparam>
    public class ProcessLinkedDbEntity_RangeCondition<TId, TEntity> :
        IInMemoryCondition<TEntity, TId>,
        IQueryableCondition<TEntity, ICollection<TId>>
        where TEntity : IProcessLinked<TId>
    {
        public bool Check(TEntity source, TId parameters)
        {
            return Comparer<TId>.Default.Compare(source.ProcessId, parameters) == 0;
        }

        public IEnumerable<TEntity> ApplayEnumerable(IEnumerable<TEntity> source, TId parameters)
        {
            return source.Where(e => Check(e, parameters));
        }

        public IQueryable<TEntity> ApplayQuery(
            IQueryable<TEntity> source,
            ICollection<TId> parameters)
        {
            return source.Where(e => parameters.Contains(e.ProcessId));
        }
    }
}
