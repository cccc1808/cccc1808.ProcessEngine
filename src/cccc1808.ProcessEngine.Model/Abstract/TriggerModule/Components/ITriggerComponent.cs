using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components
{
    public interface ITriggerComponent<TId>
    {
        /// <summary>
        /// Ключ триггера.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Тип.
        /// </summary>
        TriggerKind Kind { get; }

        /// <summary>
        /// Счетчик.
        /// </summary>
        int? Counter { get; set; }

        /// <summary>
        /// Ключ процесса.
        /// </summary>
        TId ProcessId { get; }

        /// <summary>
        /// Активирован.
        /// * Активация тригерия при поступлении <see cref="ITriggerEvent"/>.
        /// * Активация тригера после первого срабатывания (Repeat trigger).
        /// * Активация триггера вручную в БД.
        /// </summary>
        bool IsActivated { get; set; }

        /// <summary>
        /// Триггер завершен.
        /// </summary>
        bool IsCompleted { get; set; }

        /// <summary>
        /// Таймер / задержка выполнения хендлера.
        /// * Реализация таймеров.
        /// * Реализация систем с накоплением (задержкой), чтобы не спамить.
        /// </summary>
        DateTimeOffset TimerDate { get; set; }

        /// <summary>
        /// Ключ хендлера действия.
        /// </summary>
        string HandlerKey { get; }

        DateTimeOffset SelectLockTimeout { get; set; }

        /// <summary>
        /// Только для <see cref="TriggerKind.StreamsTrigger"/>.
        /// Стрим находится в состоянии <see cref="ProcessStatusEnum.AsyncExecute"/> или <see cref="ProcessStatusEnum.WaitEvent"/>.
        /// </summary>
        bool? StreamsProcessIsWaiting { get; set; }

        /// <summary>
        /// Только для <see cref="TriggerKind.StreamsTrigger"/>.
        /// Данные о последних (наибольших) timestamp поступивших в стримы.
        /// </summary>
        Dictionary<string, long>? StreamsTimeStamp { get; set; }

        /// <summary>
        /// Только для <see cref="TriggerKind.StreamsTrigger"/>.
        /// Данные о последних (наибольших) timestamp, которые процесс обработал.
        /// </summary>
        Dictionary<string, long>? StreamProcessTimestamps { get; set; }

        public enum TriggerKind 
        {
            Counter,
            Timer,
            StreamsTrigger,
        }
    }
}
