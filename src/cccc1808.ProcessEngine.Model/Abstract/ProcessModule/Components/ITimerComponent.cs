using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components
{
    /// <summary>
    /// Компонент для взаимодействия с таймера процесса.
    /// В транзакции создается timer trigger и данные о таймере записываются в процесс <see cref="CreateTimer(string, DateTimeOffset)"/>.
    /// Позволяет проверить наличие активированных таймеров.
    /// </summary>
    public interface ITimerComponent
    {
        void CreateTimer(string key, DateTimeOffset date);

        bool TryGetTimer(string key, out DateTimeOffset date);

        void RemoveTimer(string key);

        /// <summary>
        /// Есть сработавшие таймере (нужно обработать и либо обновить дату, либо удалить таймер).
        /// </summary>
        /// <param name="now"></param>
        /// <param name="timers">Перечень активированных таймеров.</param>
        /// <returns></returns>
        bool TryGetActivatedTimers(DateTimeOffset now, out IDictionary<string, DateTimeOffset> timers);
    }
}
