using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Components
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
        bool HaveErrorFlag { get; set; }

        ProcessStatusEnum Status { get; set; }

        #endregion


        #region error

        short? ReTryCount { get; set; }

        ErrorDto? Error { get; set; }

        public readonly record struct ErrorDto(
            JsonElement ErrorJson,
            Guid SessionId,
            DateTimeOffset Date);

        #endregion


        #region Timer

        DateTimeOffset TimerDate { get; set; }

        int WakeupLockCounter { get; set; }

        #endregion
    }
}
