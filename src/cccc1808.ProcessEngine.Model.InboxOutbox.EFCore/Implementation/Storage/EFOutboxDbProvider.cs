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
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto.Registry;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.Components;
using cccc1808.ProcessEngine.Model.MessageStream.EFCore.Abstract.Componenets;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.Storage
{
    internal class EFOutboxDbProvider<TId>
        : IProcessDbProvider<TId>
    {
        private readonly IEFDbContext _dbContext;
        private readonly OutboxRegistryDto _outboxRegistry;
        private readonly int _messagesLimit;
        private readonly IId_RangeCondition<TId, OutboxProcessDataDbEntity<TId>> _id_RangeCondition;
        private readonly IMessageDbEntity_ForProcessgByStream1_RangeCondition<TId, OutboxMessageDbEntity<TId>> _selectForProcessingCondition;

        public EFOutboxDbProvider(
            IEFDbContext dbContext, 
            OutboxRegistryDto outboxRegistry,
            int messagesLimit)
        {
            _dbContext = dbContext;
            _outboxRegistry = outboxRegistry;
            _messagesLimit = messagesLimit;
            _id_RangeCondition = new IId_RangeCondition<TId, OutboxProcessDataDbEntity<TId>>();
            _selectForProcessingCondition = new IMessageDbEntity_ForProcessgByStream1_RangeCondition<TId, OutboxMessageDbEntity<TId>>();
        }

        public async Task LoadForAsyncProcessingAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            var outboxProcesses = byTypeIndex[_outboxRegistry.ProcessType];

            // 1) data
            var outboxData = await _dbContext.Set<OutboxProcessDataDbEntity<TId>>()
                .Include(e => e.Queue)
                .ApplayFilterCondition(_id_RangeCondition, outboxProcesses)
                .ToDictionaryAsync(e => e.ProcessId, e => e, cancellationToken);

            // 2) messages batch.
            var messages = await _dbContext.Set<OutboxMessageDbEntity<TId>>()
                .ApplayFilterCondition(
                    _selectForProcessingCondition,
                    new IMessageDbEntity_ForProcessgByStream1_RangeCondition<TId, OutboxMessageDbEntity<TId>>.ParamDto(
                        outboxProcesses, 
                        WithPriorityOrdering: true
                        )
                    )
                .Take(_messagesLimit)
                .ToArrayAsync(cancellationToken);

            var messagesByStream = messages
                .GroupBy(e => e.ProcessId)
                .ToDictionary(e => e.Key, e => e);

            // 3) unprocesses messages count
            var activeMessagesCount = await _dbContext.Set<OutboxMessageDbEntity<TId>>()
                .ApplayFilterCondition(
                    _selectForProcessingCondition, 
                    new IMessageDbEntity_ForProcessgByStream1_RangeCondition<TId, OutboxMessageDbEntity<TId>>.ParamDto(
                        outboxProcesses,
                        WithPriorityOrdering: false
                        )
                    )
                .GroupBy(e => e.ProcessId, (e1, e2) => new { Id = e1, ActiveMessagesCount = e2.Count() })
                .ToDictionaryAsync(e => e.Id, e => e.ActiveMessagesCount, cancellationToken);

            // TODO: проблема - мы загрузили батч процессов (с блокировкой)
            // и у некоторыз из них мы загрузили батч сообщений (но не у всех).
            // И мы удерживаем блокировку даже по тем процессам, по которым не будет обработки (подумать).

            foreach (var elem in outboxProcesses)
            {
                var process = processes[elem];
                var component = new EFOutboxComponentProxy<TId>(
                    outboxData[process.Id],
                    messagesByStream[process.Id].Select(e => new EFOutboxMessageProxy<TId>(e)).ToArray(),
                    activeMessagesCount[process.Id]);
                process.AddComponent(component);
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
            // EF дополнительное сохранение не нужно.
            return Task.CompletedTask;
        }
    }
}
