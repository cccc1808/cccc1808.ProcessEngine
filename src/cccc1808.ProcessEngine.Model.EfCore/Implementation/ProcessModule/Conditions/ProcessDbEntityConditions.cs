using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule;
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
            IInMemoryCondition<TEntity> Memory, 
            IQueryableCondition<TEntity> Query, 
            IQueryableCondition<TEntity, ICollection<TId>> QueryIds
            ) 
            WaitEvent
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
            IQueryableCondition<TEntity, IProcessDbEntityConditions<TId, TEntity>.DbProcessingForHandlerParameters> Query)
            DbProcessingForHandler
        { get; }

        public IQueryableCondition<T, TEntity, IProcessDbEntityConditions<TId, TEntity>.DbProcessingForHandlerParameters> DbProcessingForHandlerProjection<T>(
            IQueryable<T> source)
        {
            return new DelegateIQueryableCondition<T, TEntity, IProcessDbEntityConditions<TId, TEntity>.DbProcessingForHandlerParameters>(
                (q, s, p) =>
                {
                    // var collection = p.dbContext.QueryFromCollection(p.registrations);

                    return source
                        .DWhere(s, e => e.Status == ProcessStatusEnum.AsyncExecute)
                        // .DWhere(s, e => p.ids.Contains(e.Id))
                        ;
                });
        }        


        #endregion

        #region protected

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

            WaitEvent = (
                new DelegateInMemoryCondition<TEntity>(
                    (s) => s.Status == ProcessStatusEnum.WaitEvent
                    ),
                new DelegateIQueryableCondition<TEntity>(
                    (s) => s.Where(e => e.Status == ProcessStatusEnum.WaitEvent)
                    ),
                new DelegateIQueryableCondition<TEntity, ICollection<TId>>(
                    (s, ids) => s.Where(e =>
                        e.Status == ProcessStatusEnum.WaitEvent
                        && ids.Contains(e.Id))
                    )
                );

            ProcessRegistry = (
                null, 
                new DelegateIQueryableCondition<TEntity, (IEFDbContext dbContext, ICollection<ProcessRegistryDto> data)>(
                    (s, p) => 
                    {
                        throw new Exception("Использовать нормальный join.");

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
                            .Where(
                                e =>
                                e.Priority == p.registration.Priority
                                && e.ProcessTypeId == p.registration.ProcessType.ProcessType 
                                && e.ProcessVersion == p.registration.ProcessType.ProcessVersion
                                && Comparer<TId>.Default.Compare(e.Id, p.offsetId) > 0 // keyset
                                )
                            .ApplayQueryCondition(AsyncExecute.Query)
                            .OrderByDescending(e => e.Priority)
                            .ThenBy(e => e.ProcessTypeId)
                            .ThenBy(e => e.ProcessVersion)
                            .ThenBy(e => e.Id);

                        return s;
                    })
                );

            DbProcessingForHandler = (
                null,
                new DelegateIQueryableCondition<TEntity, IProcessDbEntityConditions<TId, TEntity>.DbProcessingForHandlerParameters>(
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
                        && e.LastAsyncExecuteDate < timeout // 3) Не выполнялся.
                        )                   
                    )
                );
        }        
    }
}
