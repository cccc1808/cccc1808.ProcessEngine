using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Components
{
    /// <summary>
    /// Информация о текузей сессии асинхронной обработки.
    /// (Runtime данные, не сохраняются в БД).
    /// Я вляется обязательным.
    /// </summary>
    public interface ICurrentSessionComponent
    {
        /// <summary>
        /// Идентификатор сессии обработки
        /// </summary>
        Guid SessionId { get; set; }

        /// <summary>
        /// Первый шаг сессии асинхронной обработки.
        /// </summary>
        bool IsSessionFirstStep { get; set; }

        short ReTryLimit { get; }

        /// <summary>
        /// Наличие ошибки процесса в текущей сессии
        /// </summary>
        bool HaveError { get; set; }

        /// <summary>
        /// Флаг, означающий что нужно создать ReTry таймер.
        /// </summary>
        DateTimeOffset? CreateRetryTimer { get; set; }

        /// <summary>
        /// Флаг, означающий что таймер создан.
        /// </summary>
        bool RetryTimerCreated { get; set; }
    }
}
