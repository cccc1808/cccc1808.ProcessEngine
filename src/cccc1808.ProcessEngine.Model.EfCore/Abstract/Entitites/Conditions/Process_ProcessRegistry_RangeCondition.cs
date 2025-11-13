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
    public class Process_ProcessRegistry_RangeCondition<TId, TEntity>
        : IQueryableCondition<TEntity, (IEFDbContext dbContext, ICollection<ProcessRegistryDto> data)>
        where TEntity : ProcessDbEntity<TId>
    {
        public IQueryable<TEntity> ApplayQueryable(
            IQueryable<TEntity> source, 
            (IEFDbContext dbContext, ICollection<ProcessRegistryDto> data) parameters)
        {
            var joinData = parameters
                .data
                .Select(e => new { ProcessTypeId = e.ProcessType.ProcessType, e.ProcessType.ProcessVersion, e.Priority })
                .ToArray();
            var queryList = parameters.dbContext.QueryFromCollection(joinData);

            var query = from e2 in queryList
                from e in source.Where(e1 =>
                    e1.ProcessTypeId == e2.ProcessTypeId
                    && e1.ProcessVersion == e2.ProcessVersion
                    && e1.Priority == e2.Priority)
                select e;

            return query;
        }        
    }
}
