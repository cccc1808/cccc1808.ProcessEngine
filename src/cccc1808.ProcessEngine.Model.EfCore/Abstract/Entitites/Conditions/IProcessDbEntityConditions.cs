using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto.Registry;
using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Storage;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites.Conditions
{
    public interface IProcessDbEntityConditions<TId, TEntity>
        where TEntity : ProcessDbEntity<TId>
    {
        (
            IInMemoryProjectionCondition<TEntity, TId> Projection,
            IInMemoryCondition<TEntity, ICollection<TId>> MemoryRange,
            IQueryableCondition<TEntity, ICollection<TId>> QueryRange
            ) Id
        { get; }

        (
            IInMemoryCondition<TEntity, DateTimeOffset> Memory,
            IQueryableCondition<TEntity, DateTimeOffset> Query
            ) SelectLock
        { get; }

        (
            IInMemoryCondition<TEntity, DateTimeOffset?> Memory,
            IQueryableCondition<TEntity, DateTimeOffset?> Query
            ) AsyncExecute
        { get; }

        (
            object? _no,
            IQueryableCondition<TEntity, (IEFDbContext dbContext, ICollection<ProcessRegistryDto> data)> QueryRange
            ) ProcessRegistry
        {  get; }
}
}
