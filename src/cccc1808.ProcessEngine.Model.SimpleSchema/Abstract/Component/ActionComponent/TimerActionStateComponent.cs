using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Component.ActionComponent
{
    public class TimerActionStateComponent 
        : ITokenActionStateComponent
    {
        public string Id { get; set; }

        public StatusEnum Status { get; set; }

        public DateTimeOffset? Date { get; set; }

        public string? TriggerKey { get; set; }

        public bool IgnoreSignal { get; set; }

        [Obsolete]
        public TimerActionStateComponent()
        {
            Id = default!;
        }

        public TimerActionStateComponent(
            string id,
            StatusEnum status)
        {
            Id = id;
            Status = status;
            IgnoreSignal = false;
        }

        public enum StatusEnum
        {
            /// <summary>
            /// Не активирован.
            /// </summary>
            NoActivated,
            /// <summary>
            /// Создает триггер.
            /// </summary>
            CreatingTimer,

            /// <summary>
            /// Ожидает сигнала.
            /// </summary>
            WaitSignal,

            /// <summary>
            /// Ожидает наступления даты.
            /// </summary>
            WaitingTimer,

            /// <summary>
            /// Завершен.
            /// </summary>
            Complete
        }
    }
}
