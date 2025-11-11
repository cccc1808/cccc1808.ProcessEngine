using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;

namespace cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities.Conditions
{
    internal class ProcessWakeUpDbEntity_IsAsyncExecuting_Condition<TId>
        : 
        IInMemoryCondition<ProcessWakeUpDbEntity<TId>, object?>,
        IQueryableCondition<ProcessWakeUpDbEntity<TId>, object?>
    {
        public bool Check(
            ProcessWakeUpDbEntity<TId> source, object? parameters)
        {
            return source.IsAsyncExecuting;
        }

        public IEnumerable<ProcessWakeUpDbEntity<TId>> ApplayEnumerable(
            IEnumerable<ProcessWakeUpDbEntity<TId>> source, 
            object? parameters)
        {
            return source.Where(e => Check(e, parameters));
        }

        public IQueryable<ProcessWakeUpDbEntity<TId>> ApplayQueryable(
            IQueryable<ProcessWakeUpDbEntity<TId>> source, object? parameters)
        {
            return source.Where(e => e.IsAsyncExecuting);
        }        
    }
}
