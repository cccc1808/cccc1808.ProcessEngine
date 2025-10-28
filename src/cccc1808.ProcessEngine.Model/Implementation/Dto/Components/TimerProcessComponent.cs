using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.Dto.Components
{
    public class TimerProcessComponent<TId> 
        : ITimerProcessComponent<TId>
    {
        /// <summary>
        /// Дата таймера.
        /// </summary>
        public DateTimeOffset TimerDate { get; set; }
        /// <summary>
        /// Связанный процесс.
        /// </summary>
        public TId? LinkedProcessId { get; set; }
        /// <summary>
        /// True - Process, False - Timer.
        /// </summary>
        public bool IsProcessOrTimer { get; set; }
        /// <summary>
        /// Связанный процесс.
        /// </summary>
        public IProcessContainer<TId>? LinkedProcess { get; set; }        
    }
}
