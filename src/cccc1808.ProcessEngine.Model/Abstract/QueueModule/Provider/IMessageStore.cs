using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider
{
    public interface IMessageStore
    {
        Task<IDictionary<MessageIdDto, MessageDto>> GetMessagesAsync(
            MessageIdDto[] keys,
            CancellationToken cancellationToken
            );

        public readonly record struct MessageIdDto(
            string Queue,
            string IdempotencyId,
            int? PartitionId,
            long? Offset
            );
    }
}
