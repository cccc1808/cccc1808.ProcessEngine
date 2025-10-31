using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Abstract.Storage.Repository;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities;
using cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.Services
{
    public class InboxHandler1<TId, TDbContext> 
        : BaseEFChangeTrackerIJobHandler1<TId, TDbContext>
        where TDbContext : DbContext
    {
        private readonly IInboxHandlerFactory<TId> _inboxHandlerFactory;
        private readonly MessageDbEntity_ForProcessgByStream1_RangeCondition<TId> messageDbEntity_ForProcessgByStream1_RangeCondition;

        public InboxHandler1(
            TDbContext dbContext,
            IProcessRepository<TId> repository,
            IProcessSetter setter,
            ILockQueryHintStore lockQueryHintStore,
            IInboxHandlerFactory<TId> inboxHandlerFactory,
            IMessageStreamTechService<TId> messageStreamTechService)
            : base(
                  dbContext,
                  repository,
                  setter)
        {
            _lockQueryHintStore = lockQueryHintStore;
            _inboxHandlerFactory = inboxHandlerFactory;
            _messageStreamTechService = messageStreamTechService;
            messageDbEntity_ForProcessgByStream1_RangeCondition = new MessageDbEntity_ForProcessgByStream1_RangeCondition<TId>();           
        }

        public override async ValueTask HandleRangeAsync(
            IReadOnlyDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            await _messageStreamTechService.BeforeStreamExecuteAsync(
                processes.Values.ToArray(), 
                cancellationToken);

            var inboxComponenets = processes.Values
                .Select(e => e.GetComponent<InboxProcessDataDbEntity<TId>>())
                .ToDictionary(e => e.Id, e => e);

            (MessageDbEntity<TId> StreamMessage, InboxMessageDbEntity<TId> InboxMessage)[] messages;
            using (var hintScope = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                var data = await _dbContext.Set<MessageDbEntity<TId>>()
                    .ApplayFilterCondition(
                        messageDbEntity_ForProcessgByStream1_RangeCondition,
                        processes.Values.Select(e => e.Id).ToArray()
                        )
                    .Join(
                        _dbContext.Set<InboxMessageDbEntity<TId>>(),
                        e => e.Id,
                        e => e.Id,
                        (e1, e2) => new { e1, e2 })
                    .Take(250)
                    .ToArrayAsync(cancellationToken);

                messages = data
                    .Select(e => (e.e1, e.e2))
                    .ToArray();
            }

            var groupByStream = messages
                .GroupBy(e => e.StreamMessage.StreamId)
                .ToDictionary(e => e.Key, e => e);

            // TODO: обработка ошибок
            foreach (var elem in groupByStream)
            {
                var stream = (Process: processes[new ProcessIdDto<TId>(elem.Key)], Data: inboxComponenets[elem.Key]);
                var messageBatch = elem.Value
                    .OrderByDescending(e => e.StreamMessage.Priority)
                    .ThenBy(e => e.StreamMessage.OrderId)
                    .Select(e => new MessageDto())                        
                    .ToArray();

                try
                {                    
                    var handler = _inboxHandlerFactory.GetHandler(stream.Data);
                    await handler.HandleAsync(
                        stream.Data,
                        messageBatch,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _setter.SetError(stream.Process, ex);
                }
            }

            // Сбрасываем selectDate т.к. мы могли обработать не все сообщения во всех стрим, а блокировку на них сейчас держим.
            // Можно сделать более хитрую политику.
            foreach (var elem in processes.Values)
            {
                if (elem.CurrentSession.HaveError)
                {
                    continue;
                }

                _setter.SetTimer(elem, DateTimeOffset.MinValue.UtcDateTime);
            }

            await _messageStreamTechService.AfterStreamExecuteAsync(
                processes.Values.ToArray(),
                cancellationToken);
        }
    }
}
