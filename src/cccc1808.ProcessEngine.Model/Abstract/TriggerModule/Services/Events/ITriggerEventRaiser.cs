using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events
{
    /// <summary>
    /// Интерфейс для публикации событий по триггеру.
    /// 
    /// Info: Можно сделать реализацию пол БД, тогда ITriggerEvent гарантировано не будет теряться,
    /// но сильно увеличит нагрузку на БД. Первоначальная задумка имено в реализации через брокер.
    /// </summary>
    public interface ITriggerEventRaiser<TId>
    {
        ValueTask RaiseAsync(
            ICollection<RaiseContainer> events,
            CancellationToken cancellationToken);

        void ClearBuffer();

        public readonly record struct RaiseContainer(
            string EventQueue,
            TId ProcessId,
            ITriggerEvent Event,
            bool UsePersist = false
            );
    }
}
