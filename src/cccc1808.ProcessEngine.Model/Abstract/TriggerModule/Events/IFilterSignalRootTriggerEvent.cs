using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events
{
    /// <summary>
    /// Включене/Отключение игнорирования определенного типа сигнала на корневом триггере.
    /// </summary>
    public interface IFilterSignalRootTriggerEvent 
        : ITriggerEvent
    {
        /// <summary>
        /// Тип сигнала.
        /// </summary>
        ulong SignalCodeFilter { get; }
    }
}
