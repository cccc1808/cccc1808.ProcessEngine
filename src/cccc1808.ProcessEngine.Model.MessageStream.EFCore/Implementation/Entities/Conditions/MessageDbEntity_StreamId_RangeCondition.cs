using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;

namespace cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities.Conditions
{
    public class MessageDbEntity_StreamId_RangeCondition<TId>
        : IQueryableCondition<MessageDbEntity<TId>, ICollection<TId>>
    {
        public IQueryable<MessageDbEntity<TId>> ApplayQueryable(
            IQueryable<MessageDbEntity<TId>> source, 
            ICollection<TId> parameters)
        {
            return source.Where(e => parameters.Contains(e.StreamId));
        }
    }
}
