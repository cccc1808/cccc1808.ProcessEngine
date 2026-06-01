using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events
{
    /// <summary>
    /// * Сигнал для Emergency триггера.
    /// * Говорит о необходимости strean триггеру перепроверить состояние процесса т.к. событие об остановке процесса было утеряно.
    /// * Emergency триггер не обращается к strean триггеру напрямую т.к. тот может в этот момент интенсивно принимать сигналы 
    /// (и взять блокировку будет проблемно, поэтому через событие).
    /// </summary>
    public interface IRecheckProcessStatusStreamTriggerEvent
        : ITriggerEvent
    {
    }
}
