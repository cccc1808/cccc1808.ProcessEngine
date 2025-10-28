using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;

namespace cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities.Conditions
{
    internal class StreamActiveDbEntity_StreamActiveFlag_Condition<TId>
        : 
        IInMemoryCondition<StreamActiveDbEntity<TId>, object?>,
        IQueryableCondition<StreamActiveDbEntity<TId>, object?>
    {
        public bool Check(
            StreamActiveDbEntity<TId> source, object? parameters)
        {
            return source.StreamActiveFlag;
        }

        public IEnumerable<StreamActiveDbEntity<TId>> ApplayEnumerable(
            IEnumerable<StreamActiveDbEntity<TId>> source, 
            object? parameters)
        {
            return source.Where(e => Check(e, parameters));
        }

        public IQueryable<StreamActiveDbEntity<TId>> ApplayQueryable(
            IQueryable<StreamActiveDbEntity<TId>> source, object? parameters)
        {
            return source.Where(e => e.StreamActiveFlag);
        }        
    }
}
