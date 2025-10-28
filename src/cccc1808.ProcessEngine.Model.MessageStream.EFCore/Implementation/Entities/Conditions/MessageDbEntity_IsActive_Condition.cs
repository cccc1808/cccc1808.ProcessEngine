using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;

namespace cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities.Conditions
{
    public class MessageDbEntity_IsActive_Condition<TId>
        : IQueryableCondition<MessageDbEntity<TId>, object?>
    {
        public IQueryable<MessageDbEntity<TId>> ApplayQueryable(IQueryable<MessageDbEntity<TId>> source, object? parameters)
        {
            return source.Where(e => e.IsActive);
        }
    }
}
