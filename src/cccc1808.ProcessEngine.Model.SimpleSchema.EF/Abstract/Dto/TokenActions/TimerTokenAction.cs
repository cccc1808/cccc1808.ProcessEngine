using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions
{
    /// <summary>
    /// Описывает действие или переход, привязаный к таймеру (задержке).
    /// </summary>
    public class TimerTokenAction 
        : BaseTokenAction
    {
        public TimeSpan Duration { get; set; }

        public string? HandlerKey { get; set; }

        public ITokenAction.TransitionDto? Transition { get; set; }
        
        [Obsolete]
        public TimerTokenAction()
        {
        }

        public TimerTokenAction(
            string id,
            TimeSpan duration     
            )
            : base(
                  id,
                  null)
        {
            Duration = duration;
        }
    }
}
