using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events
{
    public interface ITriggerEvent
    {
        /// <summary>
        /// Ключ триггера.
        /// </summary>
        string TriggerKey { get; }

        /// <summary>
        /// Игнорировать задержку.
        /// </summary>
        bool IgnoreDelay { get; }  
        
        KindEnum Kind { get; }

        public enum KindEnum 
        {
            WakeupSignalEvent,

            SimpleStream_SignalEvent,
            SimpleStream_ProcessGoWaitEvent,

            OffsetStream_SignalEvent,
            OffsetStream_ProcessGoWaitEvent,
        }
    }
}
