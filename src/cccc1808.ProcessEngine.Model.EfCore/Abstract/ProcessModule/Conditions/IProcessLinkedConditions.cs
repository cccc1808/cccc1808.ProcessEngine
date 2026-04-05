using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions
{
    /// <summary>
    /// Для сущностей, связанных с процессом.
    /// </summary>
    public interface IProcessLinkedConditions<TId, TEntity>
        where TEntity : IProcessLinked<TId>
    {
        (
            object? _no,
            IQueryableCondition<TEntity, ICollection<TId>> QueryRange
            ) ProcessId
        { get; }
    }
}
