using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Model.IQueryable.ProcessModule.Conditions
{
    public class ProcessLinkedConditions<TId, TEntity>
        : IProcessLinkedConditions<TId, TEntity>
        where TEntity : IProcessLinked<TId>
    {
        public (
            object? _no, 
            IQueryableCondition<TEntity,ICollection<TId>> QueryRange
            ) ProcessId
        { get; }

        public ProcessLinkedConditions() 
        {
            ProcessId = (
                null,
                new DelegateIQueryableCondition<TEntity, ICollection<TId>>(
                    (s, p) => s.Where(e => p.Contains(e.ProcessId))));
        }
    }
}
