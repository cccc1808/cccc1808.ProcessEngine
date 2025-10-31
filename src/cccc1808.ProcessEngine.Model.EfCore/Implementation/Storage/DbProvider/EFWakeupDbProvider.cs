using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.Common.Entities.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Dto.Components;
using cccc1808.ProcessEngine.Model.Implementation.Storage;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Storage.DbProvider
{
    public class EFWakeupDbProvider<TId, TDbContext>
        : IProcessDbProvider<TId>
        where TDbContext: DbContext
    {
        private readonly TDbContext _dbContext;
        private readonly IId_RangeCondition<TId, WakeUpProcessDbEntity<TId>> _id_RangeCondition;

        public EFWakeupDbProvider(
            TDbContext dbContext, 
            IId_RangeCondition<TId, WakeUpProcessDbEntity<TId>> id_RangeCondition)
        {
            _dbContext = dbContext;
            _id_RangeCondition = id_RangeCondition;
        }

        public async Task LoadForAsyncProcessingAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            // Не блокируем т.к. система отдельно управляет блокировками.
            var data = await _dbContext.Set<WakeUpProcessDbEntity<TId>>()
                // TODO: filter process
                .ApplayFilterCondition(_id_RangeCondition, processes.Keys)
                .ToArrayAsync(cancellationToken);

            foreach (var elem in data)
            {
                var process = processes[elem.Id];

                process.AddComponent(
                    new EFWakeUpProxyComponent<TId>(elem, inAsyncExecuting: true));
            }
        }

        public async Task LoadRangeAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            bool withLock,
            CancellationToken cancellationToken)
        {
            // Не блокируем при withLock.
            var data = await _dbContext.Set<WakeUpProcessDbEntity<TId>>()
                .ApplayFilterCondition(_id_RangeCondition, processes.Keys)
                .ToArrayAsync(cancellationToken);

            foreach (var elem in data)
            {
                var process = processes[elem.Id];

                process.AddComponent<IWakeUpComponent>(
                    new EFWakeUpProxyComponent<TId>(elem, inAsyncExecuting: false));
            }
        }

        public Task UpdateAsync(
            ICollection<IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            foreach (var elem in processes)
            {
                elem.GetComponent<IWakeUpComponent>().NeedUpdate = false;
            }

            return Task.CompletedTask;
        }
    }
}
