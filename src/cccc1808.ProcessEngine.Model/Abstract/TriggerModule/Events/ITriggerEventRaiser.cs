using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events
{
    /// <summary>
    /// Интерфейс для публикации событий по триггеру.
    /// </summary>
    public interface ITriggerEventRaiser
    {
        ValueTask RaiseAsync(
            ITriggerEvent[] events,
            CancellationToken cancellationToken);
    }
}
