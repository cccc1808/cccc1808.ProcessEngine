using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;
using cccc1808.ProcessEngine.Model.Abstract.Common.Entities.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Dto.Components;
using cccc1808.ProcessEngine.Model.Implementation.Storage;
using cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Dto.Components;
using cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Dto.Registry;
using cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Storage
{
    internal class MessageStreamDbProvider<TId, TDbContext> : IProcessDbProvider<TId>
        where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;
        private readonly IReadOnlySet<ProcessTypeDto> _messageStreamRegistryDtos;
        // private readonly IId_RangeCondition<TId, StreamProcessDataDbEntity<TId>> _streamProcessDataDbEntity_id_RangeCondition;

        public MessageStreamDbProvider(
            IEnumerable<MessageStreamRegistryDto> messageStreamRegistryDtos,
            TDbContext dbContext)
        {
            _messageStreamRegistryDtos = messageStreamRegistryDtos
                .Select(e => e.Process.ProcessType)
                .ToHashSet();
            _dbContext = dbContext;
            // _streamProcessDataDbEntity_id_RangeCondition = new IId_RangeCondition<TId, StreamProcessDataDbEntity<TId>>();
        }

        public async Task LoadForAsyncProcessingAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            var messageStreamProcesses = processes.Values
                .Where(e => _messageStreamRegistryDtos.Contains(e.Process.Info.ProcessType))
                .ToArray();

            var ids = messageStreamProcesses
                .Select(e => e.Id)
                .ToArray();

            //var datas = await _dbContext.Set<StreamProcessDataDbEntity< TId >>()
            //    .ApplayFilterCondition(_streamProcessDataDbEntity_id_RangeCondition, ids)
            //    .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

            foreach (var elem in messageStreamProcesses)
            {
                elem.AddComponent(
                    new MessageStreamComponent<TId>() 
                    { 
                        // StreamDataDbEntity = datas[elem.Id],
                        StreamProcessDbEntity = (TimerProcessDbEntity<TId>)((EFProcessProxyComponent<TId>)elem.Process).ProcessDbEntity,
                    });
            }

            throw new NotImplementedException();
        }

        public Task UpdateAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
