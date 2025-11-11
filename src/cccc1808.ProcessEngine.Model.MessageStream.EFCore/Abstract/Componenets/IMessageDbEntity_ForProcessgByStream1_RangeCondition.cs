using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Condition;

namespace cccc1808.ProcessEngine.Model.MessageStream.EFCore.Abstract.Componenets
{
    /// <summary>
    /// Отбор сообщений для обработки на основании идентификаторов стримов.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class IMessageDbEntity_ForProcessgByStream1_RangeCondition<TId, TEntity>
        : IQueryableCondition<TEntity, IMessageDbEntity_ForProcessgByStream1_RangeCondition<TId, TEntity>.ParamDto>
        where TEntity : IMessageDbEntity<TId>
    {
        public IQueryable<TEntity> ApplayQueryable(
            IQueryable<TEntity> source,
            ParamDto parm)
        {
            source = source
                .Where(e =>
                    parm.ProcessIds.Contains(e.ProcessId)
                    && e.IsActive);

            if (parm.WithPriorityOrdering)
            {
                source = source
                    .OrderByDescending(e => e.Priority)                
                    .ThenBy(e => e.OrderId);
            }

            return source;
        }

        public readonly record struct ParamDto(
            ICollection<TId> ProcessIds,
            bool WithPriorityOrdering
            );
    }
}
