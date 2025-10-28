using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;

namespace cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities.Conditions
{
    internal class WakeUpProcessDbEntity_IsAsyncExecuting_Condition<TId>
        : 
        IInMemoryCondition<WakeUpProcessDbEntity<TId>, object?>,
        IQueryableCondition<WakeUpProcessDbEntity<TId>, object?>
    {
        public bool Check(
            WakeUpProcessDbEntity<TId> source, object? parameters)
        {
            return source.IsAsyncExecuting;
        }

        public IEnumerable<WakeUpProcessDbEntity<TId>> ApplayEnumerable(
            IEnumerable<WakeUpProcessDbEntity<TId>> source, 
            object? parameters)
        {
            return source.Where(e => Check(e, parameters));
        }

        public IQueryable<WakeUpProcessDbEntity<TId>> ApplayQueryable(
            IQueryable<WakeUpProcessDbEntity<TId>> source, object? parameters)
        {
            return source.Where(e => e.IsAsyncExecuting);
        }        
    }
}
