using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Conditions;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.ClassifierModule.Conditions
{
    public class AggregateClassifierDbEntityCondition<TId>
        : IAggregateClassifierDbEntityCondition<TId>
    {
        public (
            object? _no,
            IQueryableCondition<AggregateClassifierDbEntity<TId>, (IEFDbContext context, ICollection<AggregateDto> ids)> QueryRange
            ) AggregateDto
        { get; }

        public AggregateClassifierDbEntityCondition()
        {
            AggregateDto = (
                null,
                new DelegateIQueryableCondition<AggregateClassifierDbEntity<TId>, (IEFDbContext context, ICollection<AggregateDto> ids)>(
                    (s, p) => 
                    {
                        var queryList = p.context.QueryFromCollection(
                
                            p.ids                    
                            .Select(e => new { e.AggregateType, e.AggregateId })                    
                            .ToArray());

                        var query = 
                            from e1 in s
                            from e2 in queryList.Where(e2 =>
                                e1.AggregateType == e2.AggregateType
                                && e1.AggregateId == e2.AggregateId)
                            select e1;

                        return query;
                    }
                    )
                );
        }
    }
}
