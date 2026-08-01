using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider
{
    public interface ITriggerQueue<TId>
    {
        Task<List<MessageDto>> ConsumeRangeTriggersAsync(
            int batchLimit,
            int uniqueHandlersLimit,
            TimeSpan timeout,
            CancellationToken cancellationToken);

        Task<List<MessageDto>> ConsumeSignleTriggersAsync(
            int batchLimit,
            TimeSpan timeout,
            CancellationToken cancellationToken);

        Task<bool> ProduceActivatedTriggerAsync(
            ICollection<MessageContainer> messages,
            CancellationToken cancellationToken);

        public readonly record struct MessageContainer(
            in MessageDto Message,
            bool isRangeTrigger);

        public readonly record struct MessageDto(
            TId TriggerId,
            string HandlerKey);
    }
}
