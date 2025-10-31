using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.MessageStream.EFCore.Abstract;

namespace cccc1808.ProcessEngine.Model.MessageStream.EFCore.Implementation.Entities.Conditions
{
    /// <summary>
    /// Отбор сообщений для обработки на основании идентификаторов стримов.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class MessageDbEntity_ForProcessgByStream1_RangeCondition<TId, TEntity>
        : IQueryableCondition<TEntity, ICollection<TId>>
        where TEntity : IMessageDbEntity<TId>
    {
        public IQueryable<TEntity> ApplayQueryable(
            IQueryable<TEntity> source,
            ICollection<TId> parameters)
        {
            return source
                .Where(e =>
                    parameters.Contains(e.StreamProcessId)
                    && e.IsActive)
                .OrderByDescending(e => e.Priority)
                .ThenBy(e => e.OrderId);
        }
    }
}
