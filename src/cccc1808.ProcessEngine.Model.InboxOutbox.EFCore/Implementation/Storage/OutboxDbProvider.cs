using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;
using cccc1808.ProcessEngine.Model.Abstract.Common.Entities.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Implementation.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto.Registry;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.Storage
{
    internal class OutboxDbProvider<TId, TDbContext>
        : IProcessDbProvider<TId>
        where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;
        private readonly OutboxRegistryDto _outboxRegistry;
        private readonly IId_RangeCondition<TId, OutboxStreamDataDbEntity<TId>> _id_RangeCondition;

        public OutboxDbProvider(
            TDbContext dbContext, 
            OutboxRegistryDto outboxRegistry)
        {
            _dbContext = dbContext;
            _outboxRegistry = outboxRegistry;
            _id_RangeCondition = new IId_RangeCondition<TId, OutboxStreamDataDbEntity<TId>>();
        }

        public async Task LoadForAsyncProcessingAsync(
            IDictionary<TId, IProcessContainer<TId>> processes, 
            CancellationToken cancellationToken)
        {
            var outboxProcesses = processes.Values
                .Where(e => _outboxRegistry.ProcessType.ProcessType == e.Process.Info.ProcessType.ProcessType)
                .ToDictionary(e => e.Id, e => e);

            var data = await _dbContext.Set<OutboxStreamDataDbEntity<TId>>()
                .Include(e => e.Queue)
                .ApplayFilterCondition(
                    _id_RangeCondition, 
                    outboxProcesses.Keys.ToArray()
                    )
                .ToArrayAsync();

            foreach (var elem in data)
            {
                var process = outboxProcesses[elem.Id];
                process.AddComponent(elem);
            }
        }

        public Task UpdateAsync(
            ICollection<IProcessContainer<TId>> processes, 
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
