using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Handlers;

namespace cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto
{
    public enum WakeupStateEnum
    {
        /// <summary>
        /// Механизм wakeup не используется.
        /// </summary>
        NoWakeup,

        /// <summary>
        /// Механизм wakeup используется.
        /// * Задействовано отдельная таблица для обработки concurrency.
        /// * Сначала береться блокировка, потом проверяется услове <see cref="IWakeupCheckHandler{TId}"/>.
        /// </summary>
        WakeupWithState,

        /// <summary>
        /// Механизм wakeup используется.
        /// * Отдельная таблица не используется (низкий или отсуствие concurrency).
        /// * Условие <see cref="IWakeupCheckHandler{TId}"/> проверяется без дополнительной блокировки.
        /// * Может использовать для триггера <see cref="ITriggerComponent.TriggerKind.SimpleStream"/> т.к. 
        /// тригер знает о состоянии стрима и конкуренции не будет.
        /// </summary>
        WakeupWithoutState,        
    }
}
