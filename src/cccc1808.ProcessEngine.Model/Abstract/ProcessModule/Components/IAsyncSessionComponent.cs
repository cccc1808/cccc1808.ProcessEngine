using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components
{
    /// <summary>
    /// Информация о текущей сессии асинхронной обработки.
    /// (Runtime данные, не сохраняются в БД).
    /// Является обязательным при ассинхронной обработке.
    /// </summary>
    public interface IAsyncSessionComponent
    {
        /// <summary>
        /// Идентификатор сессии асинъронной обработки.
        /// </summary>
        Guid SessionId { get; set; }

        /// <summary>
        /// Первый шаг сессии асинхронной обработки.
        /// </summary>
        bool IsSessionFirstStep { get; set; }

        /// <summary>
        /// Лимит количества попыток.
        /// </summary>
        short RetryLimit { get; }

        /// <summary>
        /// Наличие ошибки процесса в текущей сессии асинхронной обработки.
        /// </summary>
        bool CurrentSessionHaveError { get; set; }

        /// <summary>
        /// Наличие ошибки в начале сессии.
        /// </summary>
        bool HaveErrorOnStart { get; }

        /// <summary>
        /// Необходимость сохранить ошибку в БД.
        /// </summary>
        bool NeedUpdateErrorData { get; set; }

        /// <summary>
        /// Взведение флага говорит движку прекратить асинхронную обработку экземпляра процесса.
        /// </summary>
        bool StopAsyncProcessingSession { get; set; }
    }
}
