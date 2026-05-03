using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Handlers;

namespace cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services
{
    /// <summary>
    /// Регистрационные метаданные о процессах,
    /// использующих механизм <see cref="IWakeupService{TId}"/>.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IWakeupRegistry<TId>
    {
        /// <summary>
        /// Использует ли процесс механизм.
        /// </summary>
        /// <param name="processType"></param>
        /// <returns></returns>
        WakeupStateEnum CheckWakeup(ProcessTypeDto processType);

        /// <summary>
        /// Получить реализацию хендлера для процесса.
        /// </summary>
        IWakeupCheckHandler<TId> GetCheckHandler(
            IServiceProvider serviceProvider,
            ProcessTypeDto processType);
    }
}
