using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components
{
    public class OffsetStreamTriggerComponent 
        : IOffsetTriggerComponent,
        IInmemoryMutableState
    {
        public string TriggerEventQueue { get; }

        public IDictionary<string, long> ProcessedOffsets { get; private set; } 
            = new Dictionary<string, long>(5);

        public OffsetStreamTriggerComponent(string triggerEventQueue)
        {
            TriggerEventQueue = triggerEventQueue;
        }

        public long UpdateMaxTimestamp(string triggerKey, long offset)
        {
            if (ProcessedOffsets.TryGetValue(triggerKey, out var exsist))
            {
                var value = Math.Max(offset, exsist);
                ProcessedOffsets[triggerKey] = value;
                return value;
            }
            else 
            {
                ProcessedOffsets.Add(triggerKey, offset);
                return offset;
            }
        }

        #region IInmemoryMutableState

        public IInmemoryMutableState.ISnapshot Capture()
        {
            return IInmemoryMutableState.JsonSnapshot.Create(this);
        }

        public void Restore(IInmemoryMutableState.ISnapshot snapshot)
        {
            var snap = IInmemoryMutableState.JsonSnapshot.Restore<OffsetStreamTriggerComponent>(
                (IInmemoryMutableState.JsonSnapshot)snapshot);
            ProcessedOffsets = snap.ProcessedOffsets;
        }

        #endregion
    }
}
