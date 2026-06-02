using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events
{
    /// <summary>
    /// Событие подтверждение доставки отправленного сигнала <see cref="ISignalSimpleStreamTriggerEvent"/>.
    /// Посылается корневым триггером в ответ на сигнал от дочернего триггера для подтверждения доставки.
    /// </summary>
    public interface IDeliveryResultEvent
        : ITriggerEvent
    {
        long Timestamp { get; }
    }
}
