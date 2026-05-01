using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Dto;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Test2.TestGroup4.Infrastructure.Services
{
    internal class BuisnessEntityForInboxDbProvider
        : IProcessDbProvider<Guid>
    {
        private readonly IEFDbContext _dbContext;
        private readonly InboxRegistryDto _inboxRegistryDto;

        public BuisnessEntityForInboxDbProvider(
            IEFDbContext dbContext, 
            InboxRegistryDto inboxRegistryDto)
        {
            _dbContext = dbContext;
            _inboxRegistryDto = inboxRegistryDto;
        }

        public async Task LoadProcessDataAsync(
            IDictionary<Guid, IProcessContainer<Guid>> processes,
            IDictionary<ProcessTypeDto, ICollection<Guid>> byTypeIndex, 
            bool isAsyncExecution, 
            CancellationToken cancellationToken)
        {
            if (!isAsyncExecution)
            {
                return;
            }

            if (!byTypeIndex.TryGetValue(_inboxRegistryDto.Registry.ProcessType, out var ids))
            {
                return;
            }

            // Есть возможность предзагрузить entity по всем процессам (если несколько процессов в одной транзакции).
            var buffer = new Dictionary<Guid, List<MessageState>>();
            foreach (var elem in ids.Select(e => processes[e]).Select(e => e))
            {
                var inbox = elem.GetComponent<IInboxComponent<Guid>>();
                var state = new MessagesStateComponent<MessageState>();

                foreach (var elem2 in inbox.Messages)
                {
                    var messageState = new MessageState()
                    {
                        Message = System.Text.Json.JsonSerializer.Deserialize<Message1Dto>(elem2.Body),
                        BuisnessDbEntity = null!,
                    };                    

                    if (!buffer.TryGetValue(messageState.Message.BuisnessEntityId, out var c))
                    {
                        c = new List<MessageState>();
                        buffer.Add(messageState.Message.BuisnessEntityId, c);
                    }
                    c.Add(messageState);

                    state.State.Add(
                        elem2.Key,
                        messageState);
                }

                elem.AddComponent(state);
            }
            
            var entities = await _dbContext.Set<BuisnessDbEntity>()
                .Where(e => buffer.Keys.Contains(e.Id))
                .ToArrayAsync(cancellationToken);

            foreach (var elem in entities)
            {
                foreach (var elem2 in buffer[elem.Id])
                {
                    elem2.BuisnessDbEntity = elem;
                }
            }            
        }        

        public Task UpdateAsync(
            ICollection<IProcessContainer<Guid>> processes, 
            IDictionary<ProcessTypeDto, ICollection<Guid>> byTypeIndex, 
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
