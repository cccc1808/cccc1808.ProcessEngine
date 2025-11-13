using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.Common.Entities.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Storage;
using cccc1808.ProcessEngine.Model.Implementation.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components.Inbox;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto.Registry;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.Components;
using cccc1808.ProcessEngine.Model.MessageStream.EFCore.Abstract.Componenets;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation
{
    public class EFInboxDbProvider<TId>
        : IProcessDbProvider<TId>
    {
        private readonly IEFDbContext _dbContext;
        private readonly InboxRegistryDto _inboxRegistryDto;
        private readonly int _messagesLimit;
        private readonly IId_RangeCondition<TId, InboxProcessDataDbEntity<TId>> _id_RangeCondition;
        private readonly IMessageDbEntity_ForProcessgByStream1_RangeCondition<TId, InboxMessageDbEntity<TId>> _selectForProcessingCondition;

        public EFInboxDbProvider(
            IEFDbContext dbContext, 
            InboxRegistryDto inboxRegistryDto,
            int messagesLimit)
        {
            _dbContext = dbContext;
            _inboxRegistryDto = inboxRegistryDto;
            _messagesLimit = messagesLimit;
            _id_RangeCondition = new IId_RangeCondition<TId, InboxProcessDataDbEntity<TId>>();
            _selectForProcessingCondition = new IMessageDbEntity_ForProcessgByStream1_RangeCondition<TId, InboxMessageDbEntity<TId>>();
        }

        public async Task LoadForAsyncProcessingAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            var inboxProcesses = byTypeIndex[_inboxRegistryDto.ProcessType];

            // 1) data
            var inboxData = await _dbContext.Set<InboxProcessDataDbEntity<TId>>()
                .Include(e => e.Queue)
                .Include(e => e.AggregateId)
                .ApplayFilterCondition(_id_RangeCondition, inboxProcesses)
                .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

            // 2) messages batch.
            var messages = await _dbContext.Set<InboxMessageDbEntity<TId>>()
                .ApplayFilterCondition(
                    _selectForProcessingCondition, 
                    new IMessageDbEntity_ForProcessgByStream1_RangeCondition<TId, InboxMessageDbEntity<TId>>.ParamDto(
                        inboxProcesses,
                        WithPriorityOrdering: true
                        )
                    )
                .Take(_messagesLimit)
                .ToArrayAsync(cancellationToken);

            var messagesByStream = messages
                .GroupBy(e => e.ProcessId)
                .ToDictionary(e => e.Key, e => e);

            // 3) unprocesses messages count
            var activeMessagesCount = await _dbContext.Set<InboxMessageDbEntity<TId>>()
                .ApplayFilterCondition(
                    _selectForProcessingCondition,
                    new IMessageDbEntity_ForProcessgByStream1_RangeCondition<TId, InboxMessageDbEntity<TId>>.ParamDto(
                        inboxProcesses,
                        WithPriorityOrdering: false
                        )
                    )
                .GroupBy(e => e.ProcessId, (e1, e2) => new { Id = e1, ActiveMessagesCount = e2.Count() })
                .ToDictionaryAsync(e => e.Id, e => e.ActiveMessagesCount, cancellationToken);

            // 4) Загрузка необходимых бизнес агрегатов, типизация сообщений (десереализация в нужный тип).
            // ...

            foreach (var elem in inboxProcesses)
            {
                var process = processes[elem];

                process.AddComponent(
                    new EFInboxComponentProxy<TId>(
                        inboxData[process.Id],
                        messagesByStream[process.Id]
                            .Select(e => (IInboxMessageComponent<TId>)new EFInboxMessageProxy<TId>(e))
                            .ToArray(),
                        activeMessagesCount[process.Id]
                        ));
            }
        }

        public async Task LoadRangeAsync(
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
            // EF дополнительное сохранение не нужно.
            return Task.CompletedTask;
        }
    }
}
