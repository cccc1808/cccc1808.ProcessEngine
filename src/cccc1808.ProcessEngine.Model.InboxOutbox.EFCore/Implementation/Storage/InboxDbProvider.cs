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

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation
{
    public class EFInboxDbProvider<TId, TDbContext>
        : IProcessDbProvider<TId>
        where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;
        private readonly InboxRegistryDto _inboxRegistryDto;
        private readonly int _messagesLimit;
        private readonly IId_RangeCondition<TId, InboxProcessDataDbEntity<TId>> _id_RangeCondition;
        private readonly MessageDbEntity_ForProcessgByStream1_RangeCondition<TId, InboxMessageDbEntity<TId>> _selectForProcessingCondition;

        public async Task LoadForAsyncProcessingAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            var inboxProcesses = byTypeIndex[_inboxRegistryDto.ProcessType];

            var inboxData = await _dbContext.Set<InboxProcessDataDbEntity<TId>>()
                .ApplayFilterCondition(_id_RangeCondition, inboxProcesses)
                .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

            var messages = await _dbContext.Set<InboxMessageDbEntity<TId>>()
                .ApplayFilterCondition(_selectForProcessingCondition, inboxProcesses)
                .Take(_messagesLimit)
                .ToArrayAsync(cancellationToken);

            var messagesByStream = messages
                .GroupBy(e => e.StreamProcessId)
                .ToDictionary(e => e.Key, e => e);

            var activeMessagesCount = await _dbContext.Set<InboxMessageDbEntity<TId>>()
                .ApplayFilterCondition(_selectForProcessingCondition, inboxProcesses)
                .GroupBy(e => e.StreamProcessId, (e1, e2) => new { Id = e1, ActiveMessagesCount = e2.Count() })
                .ToDictionaryAsync(e => e.Id, e => e.ActiveMessagesCount, cancellationToken);

            foreach (var elem in inboxProcesses)
            {
                var process = processes[elem];

                process.AddComponent(
                    new InboxProcessComponent<TId>() 
                    { 
                        Data = inboxData[process.Id],
                        Messages = messagesByStream[process.Id].ToArray(),
                        UnreadCount = activeMessagesCount[process.Id]
                    });
            }
        }

        public async Task LoadRangeAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            bool withLock,
            CancellationToken cancellationToken)
        {
            var inboxProcesses = byTypeIndex[_inboxRegistryDto.ProcessType];

            var activeMessagesCount = await _dbContext.Set<InboxMessageDbEntity<TId>>()
                .ApplayFilterCondition(_selectForProcessingCondition, inboxProcesses)
                .GroupBy(e => e.StreamProcessId, (e1, e2) => new { Id = e1, ActiveMessagesCount = e2.Count() })
                .ToDictionaryAsync(e => e.Id, e => e.ActiveMessagesCount, cancellationToken);

            foreach (var elem in inboxProcesses)
            {
                var process = processes[elem];

                process.AddComponent(
                    new InboxProcessComponent<TId>()
                    {
                        Messages = Array.Empty<InboxMessageDbEntity<TId>>(),
                        UnreadCount = activeMessagesCount[process.Id]
                    });
            }
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
