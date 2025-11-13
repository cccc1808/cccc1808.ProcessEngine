using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.Entities.Classifiers;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.Entities.Conditions
{
    public class AggregateClassifierDbEntity_AggregateDto_Condition<TId>
        : IQueryableCondition<AggregateClassifierDbEntity<TId>, (IEFDbContext context, ICollection<AggregateDto> ids)>
    {
        public IQueryable<AggregateClassifierDbEntity<TId>> ApplayQueryable(
            IQueryable<AggregateClassifierDbEntity<TId>> source, 
            (IEFDbContext context, ICollection<AggregateDto> ids) parameters)
        {
            var queryList = parameters.context.QueryFromCollection(
                parameters.ids
                    .Select(e => new { e.AggregateType, e.AggregateId })
                    .ToArray());

            var query = from e1 in source
            from e2 in queryList.Where(e2 =>
                e1.AggregateType == e2.AggregateType
                && e1.AggregateId == e2.AggregateId)
            select e1;

            return query;
        }
    }
}
