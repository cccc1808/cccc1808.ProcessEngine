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
    internal class ProcessWakeUpDbEntity_IsAsyncExecuting_TimerDate_RangeCondition<TId>
        : 
        IInMemoryCondition<ProcessWakeUpDbEntity<TId>, DateTimeOffset>,
        IQueryableCondition<ProcessWakeUpDbEntity<TId>, (DbContext dbContext, (TId processId, DateTimeOffset Date)[] Ids)>
    {
        public bool Check(ProcessWakeUpDbEntity<TId> source, DateTimeOffset parameters)
        {
            return 
                source.IsAsyncExecuting
                && source.TimerDate < parameters;
        }

        public IEnumerable<ProcessWakeUpDbEntity<TId>> ApplayEnumerable(
            IEnumerable<ProcessWakeUpDbEntity<TId>> source,
            DateTimeOffset parameters)
        {
            return source.Where(e => Check(e, parameters));
        }

        public IQueryable<ProcessWakeUpDbEntity<TId>> ApplayQueryable(
            IQueryable<ProcessWakeUpDbEntity<TId>> source, 
            (DbContext dbContext, (TId processId, DateTimeOffset Date)[] Ids) parameters)
        {
            var queryList = parameters.dbContext.FromLocalList(
                parameters.Ids
                    .Select(e => new { ProcessId = e.processId, Date = e.Date })
                    .ToArray(),
                typeof(MemoryJoinStubEntity),
                ValuesInjectionMethod.ViaParameters
                );

            var query = from e1 in source
            from e2 in queryList.Where(e2 => 
                e1.ProcessId.Equals(e2.ProcessId) 
                && e1.IsAsyncExecuting
                && e1.TimerDate <= e2.Date)
            select e1;

            return query;
        }        
    }
}
