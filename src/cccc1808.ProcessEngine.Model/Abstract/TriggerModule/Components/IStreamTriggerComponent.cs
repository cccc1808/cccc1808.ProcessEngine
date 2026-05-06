using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components
{
    /// <summary>
    /// Оповещает триггеры о том, что процес ушел спать.
    /// </summary>
    public interface IStreamTriggerComponent
    {
        /// <summary>
        /// Триггеры для оповещения о том, что процесс перешел в ожидание.
        /// </summary>
        string[] TriggersKeys { get; }
    }
}
