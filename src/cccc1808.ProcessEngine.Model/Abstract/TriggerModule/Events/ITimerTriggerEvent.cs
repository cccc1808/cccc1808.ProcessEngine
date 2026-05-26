using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events
{
    public interface ITimerTriggerEvent 
        : ITriggerEvent
    {
        DateTimeOffset Timer { get; }

        /// <summary>
        /// Обновить таймер, если дельта с текущей датой больше значения.
        /// Может использоваться для уменьшения количества write операций.
        /// </summary>
        TimeSpan? IfDeltaMore { get; }
    }
}
