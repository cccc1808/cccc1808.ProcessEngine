using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions
{
    public interface IProcessErrorDbEntityConditions<TId>
    {
        (
            object? _no, 
            IQueryableCondition<ProcessErrorDbEntity<TId>, ICollection<TId>> QueryRange
            ) ProcessLinkedDbEntity
        { get; }
    }
}
