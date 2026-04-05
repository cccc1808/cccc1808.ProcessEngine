using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto.Registry;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Components;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Storage
{
    internal class EFOutboxDbProvider<TId>
        : IProcessDbProvider<TId>
    {
        private readonly IEFDbContext _dbContext;
        private readonly OutboxRegistryDto _outboxRegistry;
        private readonly int _messagesLimit;

        private readonly IProcessLinkedConditions<TId, OutboxProcessDataDbEntity<TId>> _processLinkedConditions;
        private readonly IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>> _messageStreamConditions;

        public EFOutboxDbProvider(
            IEFDbContext dbContext, 
            OutboxRegistryDto outboxRegistry,
            int messagesLimit,

            IProcessLinkedConditions<TId, OutboxProcessDataDbEntity<TId>> processLinkedConditions,
            IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>> messageStreamConditions)
        {
            _dbContext = dbContext;
            _outboxRegistry = outboxRegistry;
            _messagesLimit = messagesLimit;

            _processLinkedConditions = processLinkedConditions;
            _messageStreamConditions = messageStreamConditions;
        }

        public async Task LoadForAsyncProcessingAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            var outboxProcesses = byTypeIndex[_outboxRegistry.ProcessType];

            // 1) Загружаем данные процесса.
            var outboxData = await _dbContext.Set<OutboxProcessDataDbEntity<TId>>()
                .Include(e => e.Queue)
                .ApplayQueryCondition(_processLinkedConditions.ProcessId.QueryRange, outboxProcesses)
                .ToDictionaryAsync(e => e.ProcessId, e => e, cancellationToken);

            // 2) Загружаем сообщения по процессам.
            var messages = await _dbContext.Set<OutboxMessageDbEntity<TId>>()
                .ApplayQueryCondition(
                    _messageStreamConditions.ForProcessing.Query,
                    new IMessageStreamConditions<TId, OutboxMessageDbEntity<TId>>.ForProcessingParamDto(
                        outboxProcesses, 
                        WithPriorityOrdering: true
                        )
                    )
                .Take(_messagesLimit)
                .ToArrayAsync(cancellationToken);

            var messagesByStream = messages
                .GroupBy(e => e.ProcessId)
                .ToDictionary(e => e.Key, e => e);

            // TODO: проблема - мы загрузили батч процессов (с блокировкой)
            // и у некоторыз из них мы загрузили батч сообщений (но не у всех).
            // И мы удерживаем блокировку БД даже по тем процессам, по которым не будет обработки (подумать).

            foreach (var elem in outboxProcesses)
            {
                var process = processes[elem];
                var component = new EFOutboxComponentProxy<TId>(
                    outboxData[process.Id],
                    messagesByStream[process.Id].Select(e => new EFOutboxMessageProxy<TId>(e)).ToArray());
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
