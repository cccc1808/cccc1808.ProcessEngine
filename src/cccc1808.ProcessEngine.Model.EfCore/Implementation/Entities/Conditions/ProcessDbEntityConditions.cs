using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Registry;
using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.Common.Entities.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Storage;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Entities.Conditions
{
    public class ProcessDbEntityConditions<TId, TEntity> 
        : IProcessDbEntityConditions<TId, TEntity>
        where TEntity : ProcessDbEntity<TId>
    {
        public (
            IInMemoryProjectionCondition<TEntity, TId> Projection,
            IInMemoryCondition<TEntity, ICollection<TId>> MemoryRange,            
            IQueryableCondition<TEntity, ICollection<TId>> QueryRange
            ) Id
        { get; }

        public (
            IInMemoryCondition<TEntity, DateTimeOffset> Memory, 
            IQueryableCondition<TEntity, DateTimeOffset> Query
            ) SelectLock
        { get; }

        public (
            IInMemoryCondition<TEntity, DateTimeOffset?> Memory, 
            IQueryableCondition<TEntity, DateTimeOffset?> Query
            ) AsyncExecute
        { get; }

        public (
            object? _no,
            IQueryableCondition<TEntity, (IEFDbContext dbContext, ICollection<ProcessRegistryDto> data)> QueryRange
            ) ProcessRegistry
        { get; }


        public ProcessDbEntityConditions(
            IId_RangeCondition<TId, ProcessDbEntity<TId>> id_RangeCondition)
        {
            Id = (
                new IId_RangeCondition<TId, TEntity>(),
                new IId_RangeCondition<TId, TEntity>(),
                new IId_RangeCondition<TId, TEntity>()
                );

            SelectLock = (
                new DelegateInMemoryCondition<TEntity, DateTimeOffset>((e, p) => e.SelectLock < p),
                new DelegateIQueryableCondition<TEntity, DateTimeOffset>((s, p) => s.Where(e => e.SelectLock < p))
                );

            AsyncExecute = (
                new DelegateInMemoryCondition<TEntity, DateTimeOffset?>(
                    (s, p) => p.HasValue
                        ? s.Status == ProcessStatusEnum.AsyncExecute
                        && !s.HaveErrorFlag
                        && s.TimerDate < p.Value
                        : s.Status == ProcessStatusEnum.AsyncExecute
                        && !s.HaveErrorFlag
                    ),
                new DelegateIQueryableCondition<TEntity, DateTimeOffset?>(
                    (s, p) => p.HasValue
                    ? s.Where(e =>
                        e.Status == ProcessStatusEnum.AsyncExecute
                        && !e.HaveErrorFlag
                        && e.TimerDate < p.Value)
                    : s.Where(e =>
                        e.Status == ProcessStatusEnum.AsyncExecute
                        && !e.HaveErrorFlag)
                    )
                );

            ProcessRegistry = (
                null, 
                new DelegateIQueryableCondition<TEntity, (IEFDbContext dbContext, ICollection<ProcessRegistryDto> data)>(
                    (s, p) => 
                    {
                        var joinData = p
                            .data
                            .Select(e => new { ProcessTypeId = e.ProcessType.ProcessType, e.ProcessType.ProcessVersion, e.Priority })
                            .ToArray();
                        var queryList = p.dbContext.QueryFromCollection(joinData);

                        var query = 
                            from e2 in queryList
                            from e in s.Where(e1 =>
                                e1.ProcessTypeId == e2.ProcessTypeId
                                && e1.ProcessVersion == e2.ProcessVersion
                                && e1.Priority == e2.Priority)
                            select e;

                        return query;
                    })
                );
        }

    }
}
