using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component.ActionComponent
{
    public class TimerActionStateComponent 
        : ITokenActionStateComponent
    {
        public string Id { get; set; }

        public StatusEnum Status { get; set; }

        public DateTimeOffset? Date { get; set; }

        public string? TriggerKey { get; set; }

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
        }

        public enum StatusEnum
        {
            NoActivated,
            CreatingTimer,
            WaitingTimer,
            Complete
        }
    }
}
