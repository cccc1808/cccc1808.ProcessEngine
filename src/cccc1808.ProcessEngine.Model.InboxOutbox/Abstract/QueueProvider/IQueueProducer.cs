using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.QueueProvider
{
    public interface IQueueProducer
    {
        Task ProduceBatchAsync(
            ICollection<MessageDto> messages,
            CancellationToken cancellationToken
            );
    }
}
