using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Components
{
    /// <summary>
    /// Поля процесса-таймера.
    /// (Сохраняются в БД).
    /// </summary>
    public interface ITimerProcessComponent<TId>
    {
        public DateTimeOffset TimerDate { get; set; }
        public TId? LinkedProcessId { get; set; }
        public bool IsProcessOrTimer { get; set; }
        public IProcessContainer<TId>? LinkedProcess { get; set; }
    }
}
