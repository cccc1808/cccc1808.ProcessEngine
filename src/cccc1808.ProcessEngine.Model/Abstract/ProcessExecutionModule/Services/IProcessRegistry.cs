using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services
{
    public interface IProcessRegistry
    {
        /// <summary>
        /// Все зарегистрированные процессы.
        /// </summary>
        /// <returns></returns>
        ICollection<ProcessRegistryDto> All();

        /// <summary>
        /// Использует ли процесс коды сигналов у триггеров.
        /// </summary>
        /// <param name="processType"></param>
        /// <returns></returns>
        bool UseSignalCode(ProcessTypeDto processType);
    }
}
