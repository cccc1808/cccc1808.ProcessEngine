using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider
{
    public interface IQueueProducer
    {
        Task ProduceBatchAsync(
            ICollection<MessageDto> messages,
            CancellationToken cancellationToken
            );
    }
}
