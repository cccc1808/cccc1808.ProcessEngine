using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;

namespace cccc1808.ProcessEngine.Model.Abstract.Common.Entities.Conditions
{
    public class IId_RangeCondition<TId, TIId>
        :
        IInMemoryCondition<TIId, ICollection<TId>>,
        IInMemoryProjectionCondition<TIId, TId>,
        IQueryableCondition<TIId, ICollection<TId>>
        where TIId : IId<TId>
    {
        public bool Check(TIId source, ICollection<TId> parameters)
        {
            return parameters.Contains(source.Id);
        }

        public IEnumerable<TIId> ApplayEnumerable(IEnumerable<TIId> source, ICollection<TId> parameters)
        {
            return source.Where(e => Check(e, parameters));
        }

        public TId ApplayProjection(TIId source)
        {
            return source.Id;
        }

        public IEnumerable<TId> ApplayProjectionEnumerable(IEnumerable<TIId> source)
        {
            return source.Select(ApplayProjection);
        }

        public IQueryable<TIId> ApplayQueryable(
            IQueryable<TIId> source, 
            ICollection<TId> parameters)
        {
            return source.Where(e => parameters.Contains(e.Id));
        }        
    }
}
