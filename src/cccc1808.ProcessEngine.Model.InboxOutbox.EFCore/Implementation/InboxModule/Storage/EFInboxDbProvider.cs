using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components.Inbox;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto.Registry;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Components;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Storage
{
    public class EFInboxDbProvider<TId>
        : IProcessDbProvider<TId>
    {
        private readonly IEFDbContext _dbContext;
        private readonly InboxRegistryDto _inboxRegistryDto;
        private readonly int _messagesLimit;

        private readonly IProcessLinkedConditions<TId, InboxProcessDataDbEntity<TId>> _processLinkedConditions;
        private readonly IMessageStreamConditions<TId, InboxMessageDbEntity<TId>> _messageStreamConditions;

        public EFInboxDbProvider(
            IEFDbContext dbContext, 
            InboxRegistryDto inboxRegistryDto,
            int messagesLimit,

            IProcessLinkedConditions<TId, InboxProcessDataDbEntity<TId>> processLinkedConditions,
            IMessageStreamConditions<TId, InboxMessageDbEntity<TId>> messageStreamConditions)
        {
            _dbContext = dbContext;
            _inboxRegistryDto = inboxRegistryDto;
            _messagesLimit = messagesLimit;

            _processLinkedConditions = processLinkedConditions;
            _messageStreamConditions = messageStreamConditions;
        }

        public async Task LoadForAsyncProcessingAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            var inboxProcesses = byTypeIndex[_inboxRegistryDto.ProcessType];

            // 1) Загружаем данные процесса.
            var inboxData = await _dbContext.Set<InboxProcessDataDbEntity<TId>>()
                .Include(e => e.Queue)
                .Include(e => e.Aggregate)
                .ApplayQueryCondition(_processLinkedConditions.ProcessId.QueryRange, inboxProcesses)
                .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

            // 2) Загружаем сообщения по процессам.
            var messages = await _dbContext.Set<InboxMessageDbEntity<TId>>()
                .ApplayQueryCondition(
                    _messageStreamConditions.ForProcessing.Query, 
                    new IMessageStreamConditions<TId, InboxMessageDbEntity<TId>>.ForProcessingParamDto(
                        inboxProcesses,
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

            foreach (var elem in inboxProcesses)
            {
                var process = processes[elem];

                var component = new EFInboxComponentProxy<TId>(
                    inboxData[process.Id],
                    messagesByStream[process.Id]
                        .Select(e => (IInboxMessageComponent<TId>)new EFInboxMessageProxy<TId>(e))
                        .ToArray()
                    );
                process.AddComponent(component);
            }            

            // 4) Загрузка необходимых бизнес агрегатов, типизация сообщений (десереализация в нужный тип).
            // ...
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
