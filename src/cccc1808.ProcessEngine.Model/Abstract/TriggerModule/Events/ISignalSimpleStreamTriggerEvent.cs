using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events
{
    public interface ISignalSimpleStreamTriggerEvent
        : ITriggerEvent
    {
        /// <summary>
        /// Ключ дочернего триггера, отправевшего этот сигнал на корневой триггер и
        /// ожидающего подтверждения через <see cref="IDeliveryResultEvent"/>.
        /// </summary>
        string? SendTriggerKey { get; }

        /// <summary>
        /// Отмета события для сопоставления дочерниг триггером (дублируется в ответе).
        /// </summary>
        long? SendTimeStamp { get; }
    }
}
