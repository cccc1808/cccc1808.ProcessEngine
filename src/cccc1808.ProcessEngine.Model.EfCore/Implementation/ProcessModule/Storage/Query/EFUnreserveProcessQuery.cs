using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Storage.Queries;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Query
{
    public class EFUnreserveProcessQuery<TId, TEntity> 
        : IUnreserveProcessQuery<TId>
        where TEntity : ProcessDbEntity<TId>
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEFDbContext _dbContext;
        private readonly IProcessDbEntityConditions<TId, TEntity> _processDbEntityConditions;

        public EFUnreserveProcessQuery(
            IDateTimeProvider dateTimeProvider,
            IEFDbContext dbContext, 
            IProcessDbEntityConditions<TId, TEntity> processDbEntityConditions)
        {
            _dateTimeProvider = dateTimeProvider;
            _dbContext = dbContext;
            _processDbEntityConditions = processDbEntityConditions;
        }

        public async Task UnreserveAsync(
            Queue<ProcessInstanceInfoDto<TId>> ids,
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;

            // Снимаем блокировку выборки.
            await _dbContext.Set<TEntity>()
                .ApplayQueryCondition(
                    _processDbEntityConditions.Id.QueryRange,
                    ids.Select(e => e.Id).ToArray()
                    )
                // Для оптимизации - использование фильтрующего индекса.
                .ApplayQueryCondition(_processDbEntityConditions.AsyncExecute.Query)
                .ExecuteUpdateAsync(
                    e => e.SetProperty(e => e.SelectLockTimeout, now),
                    cancellationToken);
        }

        public async Task UnreserveAsync(
            ICollection<TId> ids,
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;

            // Снимаем блокировку выборки.
            await _dbContext.Set<TEntity>()
                .ApplayQueryCondition(
                    _processDbEntityConditions.Id.QueryRange,
                    ids
                    )
                // Для оптимизации - использование фильтрующего индекса.
                .ApplayQueryCondition(_processDbEntityConditions.AsyncExecute.Query)
                .ExecuteUpdateAsync(
                    e => e.SetProperty(e => e.SelectLockTimeout, now),
                    cancellationToken);
        }
    }
}
