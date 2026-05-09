using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;

namespace cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto
{
    public enum WakeupStateEnum
    {
        /// <summary>
        /// Механизм check wakeup не используется.
        /// * Все триггеры должны блокировать процесс напрямую.
        /// </summary>
        NoWakeup,

        /// <summary>
        /// Механизм check wakeup используется.
        /// * Задействовано отдельная таблица для обработки concurrency.
        /// Сначала береться блокировка, потом проверяется условие <see cref="IWakeupCheckHandler{TId}"/>,
        /// это гарантирует что сигнал не будет потерян (он либо уже записан до блокировки, либо оповещение о нем поступит после блокировки - пробуждение от триггера).
        /// * Позволяет триггеру не конкурировать за блокировку процесса, если он выполняется, береться только share блокировка на WakeupLockTable.
        /// * Все триггеры должны вызывать <see cref="IWakeupService{TId}.WakeupProcessHandlerAsync(ICollection{TId}, bool, CancellationToken)"/> (кроме триггера ошибки).
        /// </summary>
        CheckWakeupWithLock,

        /// <summary>
        /// Механизм check wakeup используется.
        /// * Отдельная таблица не используется (низкий или отсуствие concurrency).
        /// Условие <see cref="IWakeupCheckHandler{TId}"/> проверяется без дополнительной блокировки.
        /// * Может использовать в ситуации когда используется
        /// 1 <see cref="ITriggerComponent.TriggerKind.SimpleStream"/> или <see cref="ITriggerComponent.TriggerKind.OffsetStream"/> 
        /// т.к. тригер знает о состоянии стрима и конкуренции и потери сигнала не будет.
        /// * Все триггеры должны блокировать процесс напрямую.
        /// </summary>
        CheckWakeupWithoutLock,        
    }
}
