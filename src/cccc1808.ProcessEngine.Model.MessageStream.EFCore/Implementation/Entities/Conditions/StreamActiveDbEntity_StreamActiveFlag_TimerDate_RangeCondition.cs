using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Abstract;

using EntityFrameworkCore.MemoryJoin;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities.Conditions
{
    internal class StreamActiveDbEntity_StreamActiveFlag_TimerDate_RangeCondition<TId>
        : 
        IInMemoryCondition<StreamActiveDbEntity<TId>, SheduleDateDto>,
        IQueryableCondition<StreamActiveDbEntity<TId>, (DbContext dbContext, (TId id, SheduleDateDto Date)[] Ids)>
    {
        public bool Check(StreamActiveDbEntity<TId> source, SheduleDateDto parameters)
        {
            return 
                source.StreamActiveFlag
                && source.SheduleMinDate < parameters.DateUnixMiliseconds;
        }

        public IEnumerable<StreamActiveDbEntity<TId>> ApplayEnumerable(
            IEnumerable<StreamActiveDbEntity<TId>> source,
            SheduleDateDto parameters)
        {
            return source.Where(e => Check(e, parameters));
        }

        public IQueryable<StreamActiveDbEntity<TId>> ApplayQueryable(
            IQueryable<StreamActiveDbEntity<TId>> source, 
            (DbContext dbContext, (TId id, SheduleDateDto Date)[] Ids) parameters)
        {
            var queryList = parameters.dbContext.FromLocalList(
                parameters.Ids
                    .Select(e => new { Id = e.id, Date = e.Date.DateUnixMiliseconds })
                    .ToArray(),
                typeof(MemoryJoinStubEntity),
                ValuesInjectionMethod.ViaParameters                    
                );

            var query = from e1 in source
            from e2 in queryList.Where(e2 => 
                e1.Id.Equals(e2.Id) 
                && e1.StreamActiveFlag
                && e1.SheduleMinDate <= e2.Date)
            select e1;

            return query;
        }        
    }
}
