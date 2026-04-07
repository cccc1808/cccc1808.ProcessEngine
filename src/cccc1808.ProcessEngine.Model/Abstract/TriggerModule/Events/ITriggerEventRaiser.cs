using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events
{
    /// <summary>
    /// Интерфейс для публикации событий по триггеру.
    /// 
    /// Info: Можно сделать реализацию пол БД, тогда ITriggerEvent гарантировано не будет теряться,
    /// но сильно увеличит нагрузку на БД. Первоначальная задумка имено в реализации через брокер.
    /// </summary>
    public interface ITriggerEventRaiser
    {
        ValueTask RaiseAsync(
            ITriggerEvent[] events,
            CancellationToken cancellationToken);
    }
}
