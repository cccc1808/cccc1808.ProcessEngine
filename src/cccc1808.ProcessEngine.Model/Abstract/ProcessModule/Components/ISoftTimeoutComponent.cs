using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components
{
    /// <summary>
    /// Компонент определяющий задержку мягкой остановки.
    /// Если батч обработк будет очень большой, то позволяет приостановить его на середине.
    /// Может использоваться для ограничения долгих транзакций.
    /// Не порождает Exception, служит для прерывания цикла.
    /// </summary>
    public interface ISoftTimeoutComponent
    {
        public DateTimeOffset? StopDate { get; }
    }
}
