using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services
{
    public interface ITriggerEventInboxService
    {
        ValueTask<IContext> FilterMessagesAsync(
            Dictionary<string, List<(MessageDto Message, ITriggerEvent Event)>> groupByTriggerMessages,
            Dictionary<PartitionKey, PartitionOffset> offsetsData,
            int allMessageCount,
            CancellationToken cancellationToken);

        ValueTask AfterCommitAsync(
            IContext context,
            CancellationToken cancellationToken);

        #region types

        public interface IContext
        { }

        public readonly record struct PartitionKey(
            string Queue,
            int Partition)
        {
            public override int GetHashCode()
            {
                return HashCode.Combine(Queue, Partition);
            }
        }

        public readonly record struct PartitionOffset(
            PartitionKey Key,
            long MinValue,
            long MaxValue)
        {
            public override int GetHashCode()
            {
                return Key.GetHashCode();
            }
        }

        #endregion
    }
}
