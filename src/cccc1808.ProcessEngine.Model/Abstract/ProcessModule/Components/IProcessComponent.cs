using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components
{
    /// <summary>
    /// Основные свойства процесса.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IProcessComponent<TId>
    {
        ProcessInstanceInfoDto<TId> Info { get; set; }

        #region Status

        /// <summary>
        /// Процесс содержит ошибку.
        /// </summary>
        bool StoppedByError { get; set; }

        /// <summary>
        /// Статус процесса.
        /// </summary>
        ProcessStatusEnum Status { get; set; }

        #endregion


        #region error

        /// <summary>
        /// Счетчик retry ошибки.
        /// </summary>
        short? RetryCount { get; set; }

        /// <summary>
        /// Данные ошибки.
        /// </summary>
        ErrorDto? Error { get; set; }

        public readonly record struct ErrorDto(
            JsonElement ErrorJson,
            Guid SessionId,
            DateTimeOffset Date);

        #endregion

        DateTimeOffset ReservationTimeout { get; set; }

        /// <summary>
        /// Сигналы, поступившие в процесс.
        /// </summary>
        BitFlagDto SignalCode { get; set; }

        /// <summary>
        /// Игнорируемые сигналы.
        /// </summary>
        BitFlagDto IgnoreSignals { get; set; }
    }
}
