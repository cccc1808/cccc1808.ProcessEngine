using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.ClassifierModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Conditions
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
