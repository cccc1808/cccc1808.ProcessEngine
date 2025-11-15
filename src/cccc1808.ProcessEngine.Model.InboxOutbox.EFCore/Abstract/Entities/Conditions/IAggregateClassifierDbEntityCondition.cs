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
    public interface IAggregateClassifierDbEntityCondition<TId>
    {
        (
            object? _no,
            IQueryableCondition<AggregateClassifierDbEntity<TId>, (IEFDbContext context, ICollection<AggregateDto> ids)> QueryRange
            ) AggregateDto
        { get; }
    }
}
