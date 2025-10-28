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
        #region prop

        ProcessInstanceInfoDto<TId> Info { get; set; }

        #region Status

        /// <summary>
        /// Процесс содержит ошибку.
        /// </summary>
        bool HaveErrorFlag { get; set; }

        ProcessStatusEnum Status { get; set; }

        #endregion


        short? ReTryCount { get; set; }
        JsonElement? ErrorJson { get; set; }

        #endregion


        #region Timer

        DateTimeOffset TimerDate { get; set; }

        #endregion
    }
}
