using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Conditions
{
    public class ProcessDbEntityConditions<TId, TEntity>
        : IProcessDbEntityConditions<TId, TEntity>
        where TEntity : ProcessDbEntity<TId>
    {
        #region IProcessDbEntityConditions

        public (
            // IInMemoryProjectionCondition<TEntity, TId> Projection,
            IInMemoryCondition<TEntity, ICollection<TId>> MemoryRange,            
            IQueryableCondition<TEntity, ICollection<TId>> QueryRange
            ) Id
        { get; }        

        public (
            IInMemoryCondition<TEntity> Memory, 
            IQueryableCondition<TEntity> Query,
            IQueryableCondition<TEntity, ICollection<TId>> QueryIds
            ) AsyncExecute
        { get; }        

        public (
            object? _no, 
            IQueryableCondition<TEntity, DateTimeOffset> QueryRange
            ) MaybeStoppedByTriggerEventLoosed
        { get; }

        public (
            object? no,
            IQueryableCondition<TEntity, IProcessDbEntityConditions<TId, TEntity>.DbProcessingForSelectorParameters> Query
            ) DbProcessingForSelector
        { get; }

        public (
            object? no, 
            IQueryableCondition<TEntity, IProcessDbEntityConditions<TId, TEntity>.DbProcessingForSelectorHandlerParameters> Query)
            DbProcessingForHandler
        { get; }


        #endregion

        #region protected

        /// <summary>
        /// Условие отсутсвия <see cref="ProcessDbEntity{TId}.SelectLockTimeout"/>.
        /// </summary>
        protected (
            IInMemoryCondition<TEntity, DateTimeOffset> Memory,
            IQueryableCondition<TEntity, DateTimeOffset> Query
            ) SelectLock
        { get; }

        protected (
            object? _no,
            IQueryableCondition<TEntity, (IEFDbContext dbContext, ICollection<ProcessRegistryDto> data)> QueryRange
            ) ProcessRegistry
        { get; }

        #endregion

        public ProcessDbEntityConditions(
            Id_RangeCondition<TId, ProcessDbEntity<TId>> id_RangeCondition)
        {
            Id = (
                // new Id_RangeCondition<TId, TEntity>(),
                new Id_RangeCondition<TId, TEntity>(),
                new Id_RangeCondition<TId, TEntity>()
                );

            SelectLock = (
                new DelegateInMemoryCondition<TEntity, DateTimeOffset>((e, p) => e.SelectLockTimeout < p),
                new DelegateIQueryableCondition<TEntity, DateTimeOffset>((s, p) => s.Where(e => e.SelectLockTimeout < p))
                );

            AsyncExecute = (
                new DelegateInMemoryCondition<TEntity>(
                    (s) => s.Status == ProcessStatusEnum.AsyncExecute
                    ),
                new DelegateIQueryableCondition<TEntity>(
                    (s) => s.Where(e =>e.Status == ProcessStatusEnum.AsyncExecute)
                    ),
                new DelegateIQueryableCondition<TEntity, ICollection<TId>>(
                    (s, ids) => s.Where(e => 
                        e.Status == ProcessStatusEnum.AsyncExecute
                        && ids.Contains(e.Id))
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

            DbProcessingForSelector = (
                null,
                new DelegateIQueryableCondition<TEntity, IProcessDbEntityConditions<TId, TEntity>.DbProcessingForSelectorParameters>(
                    (s, p) =>
                    {
                        s = s
                            .ApplayQueryCondition(AsyncExecute.Query)
                            .ApplayQueryCondition(ProcessRegistry.QueryRange, (p.dbContext, p.registrations))
                            .ApplayQueryCondition(SelectLock.Query, p.now)
                            .OrderByDescending(e => e.Priority);

                        return s;
                    })
                );

            DbProcessingForHandler = (
                null,
                new DelegateIQueryableCondition<TEntity, IProcessDbEntityConditions<TId, TEntity>.DbProcessingForSelectorHandlerParameters>(
                    (s, p) =>
                    {
                        s = s
                            .ApplayQueryCondition(AsyncExecute.QueryIds, p.ids)
                            .ApplayQueryCondition(ProcessRegistry.QueryRange, (p.dbContext, p.registrations));

                        return s;
                    })
                );

            MaybeStoppedByTriggerEventLoosed = (
                null,
                new DelegateIQueryableCondition<TEntity, DateTimeOffset>(
                    (s, timeout) => s.Where(e =>
                        e.Status == ProcessStatusEnum.WaitEvent // 1) Процесс в статусе ожидания.
                        && !e.StoppedByError
                        && e.RetryCount == null // 2) Процесс не в ошибке.
                        && e.SelectLockTimeout < timeout) // 3) Процесс давно не брался в обработку.                        
                    )
                );
        }
    }
}
