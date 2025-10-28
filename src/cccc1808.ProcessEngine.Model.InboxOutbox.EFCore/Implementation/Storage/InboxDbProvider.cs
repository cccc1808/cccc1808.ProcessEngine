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
    internal class InboxDbProvider<TId, TDbContext>
        : IProcessDbProvider<TId>
        where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;
        private readonly InboxRegistryDto _inboxRegistry;
        private readonly IId_RangeCondition<TId, InboxStreamDataDbEntity<TId>> _id_RangeCondition;

        public InboxDbProvider(
            TDbContext dbContext,
            InboxRegistryDto inboxRegistry)
        {
            _dbContext = dbContext;
            _inboxRegistry = inboxRegistry;
            _id_RangeCondition = new IId_RangeCondition<TId, InboxStreamDataDbEntity<TId>>();
        }

        public async Task LoadForAsyncProcessingAsync(
            IDictionary<TId, IProcessContainer<TId>> processes, 
            CancellationToken cancellationToken)
        {
            var inboxProcesses = processes.Values
                .Where(e => _inboxRegistry.ProcessType.ProcessType == e.Process.Info.ProcessType.ProcessType)
                .ToDictionary(e => e.Id, e => e);

            var data = await _dbContext.Set<InboxStreamDataDbEntity<TId>>()
                .Include(e => e.Queue)
                .ApplayFilterCondition(
                    _id_RangeCondition, 
                    inboxProcesses.Keys.ToArray()
                    )
                .ToArrayAsync();

            foreach (var elem in data)
            {
                var process = inboxProcesses[elem.Id];
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
