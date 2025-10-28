using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;

using EntityFrameworkCore.MemoryJoin;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities.Conditions
{
    internal class WakeUpProcessDbEntity_IsAsyncExecuting_TimerDate_RangeCondition<TId>
        : 
        IInMemoryCondition<WakeUpProcessDbEntity<TId>, DateTimeOffset>,
        IQueryableCondition<WakeUpProcessDbEntity<TId>, (DbContext dbContext, (TId id, DateTimeOffset Date)[] Ids)>
    {
        public bool Check(WakeUpProcessDbEntity<TId> source, DateTimeOffset parameters)
        {
            return 
                source.IsAsyncExecuting
                && source.TimerDate < parameters;
        }

        public IEnumerable<WakeUpProcessDbEntity<TId>> ApplayEnumerable(
            IEnumerable<WakeUpProcessDbEntity<TId>> source,
            DateTimeOffset parameters)
        {
            return source.Where(e => Check(e, parameters));
        }

        public IQueryable<WakeUpProcessDbEntity<TId>> ApplayQueryable(
            IQueryable<WakeUpProcessDbEntity<TId>> source, 
            (DbContext dbContext, (TId id, DateTimeOffset Date)[] Ids) parameters)
        {
            var queryList = parameters.dbContext.FromLocalList(
                parameters.Ids
                    .Select(e => new { Id = e.id, Date = e.Date })
                    .ToArray(),
                typeof(MemoryJoinStubEntity),
                ValuesInjectionMethod.ViaParameters
                );

            var query = from e1 in source
            from e2 in queryList.Where(e2 => 
                e1.Id.Equals(e2.Id) 
                && e1.IsAsyncExecuting
                && e1.TimerDate <= e2.Date)
            select e1;

            return query;
        }        
    }
}
