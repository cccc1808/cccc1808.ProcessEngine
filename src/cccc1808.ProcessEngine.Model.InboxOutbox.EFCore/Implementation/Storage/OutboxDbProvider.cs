using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.Common.Entities.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto.Registry;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.Dto.Componenets;
using cccc1808.ProcessEngine.Model.MessageStream.EFCore.Implementation.Entities.Conditions;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.Storage
{
    internal class OutboxDbProvider<TId, TDbContext>
        : IProcessDbProvider<TId>
        where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;
        private readonly OutboxRegistryDto _outboxRegistry;
        private readonly int _messagesLimit;
        private readonly IId_RangeCondition<TId, OutboxProcessDataDbEntity<TId>> _id_RangeCondition;
        private readonly MessageDbEntity_ForProcessgByStream1_RangeCondition<TId, OutboxMessageDbEntity<TId>> _selectForProcessingCondition;

        public OutboxDbProvider(
            TDbContext dbContext, 
            OutboxRegistryDto outboxRegistry)
        {
            _dbContext = dbContext;
            _outboxRegistry = outboxRegistry;
            _id_RangeCondition = new IId_RangeCondition<TId, OutboxProcessDataDbEntity<TId>>();
        }

        public async Task LoadForAsyncProcessingAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            var outboxProcesses = byTypeIndex[_outboxRegistry.ProcessType];

            var outboxData = await _dbContext.Set<OutboxProcessDataDbEntity<TId>>()
                .Include(e => e.Queue)
                .ApplayFilterCondition(_id_RangeCondition, outboxProcesses)
                .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

            var messages = await _dbContext.Set<OutboxMessageDbEntity<TId>>()
                .ApplayFilterCondition(_selectForProcessingCondition, outboxProcesses)
                .Take(_messagesLimit)
                .ToArrayAsync(cancellationToken);

            var messagesByStream = messages
                .GroupBy(e => e.StreamProcessId)
                .ToDictionary(e => e.Key, e => e);

            var activeMessagesCount = await _dbContext.Set<OutboxMessageDbEntity<TId>>()
                .ApplayFilterCondition(_selectForProcessingCondition, outboxProcesses)
                .GroupBy(e => e.StreamProcessId, (e1, e2) => new { Id = e1, ActiveMessagesCount = e2.Count() })
                .ToDictionaryAsync(e => e.Id, e => e.ActiveMessagesCount, cancellationToken);

            foreach (var elem in outboxProcesses)
            {
                var process = processes[elem];

                process.AddComponent(
                    new OutboxProcessComponent<TId>()
                    {
                        Data = outboxData[process.Id],
                        Messages = messagesByStream[process.Id].ToArray(),
                        UnreadCount = activeMessagesCount[process.Id]
                    });
            }
        }

        public Task LoadRangeAsync(
            IDictionary<TId, IProcessContainer<TId>> processes, 
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex, 
            bool withLock,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(
            ICollection<IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
