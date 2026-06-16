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

        public DateTimeOffset Date { get; set; }

        public bool IsComplete { get; set; }

        [Obsolete]
        public TimerActionStateComponent()
        {
            Id = default!;
        }

        public TimerActionStateComponent(
            string id,
            DateTimeOffset date,
            bool isComplete)
        {
            Id = id;
            Date = date;
            IsComplete = isComplete;
        }
    }
}
