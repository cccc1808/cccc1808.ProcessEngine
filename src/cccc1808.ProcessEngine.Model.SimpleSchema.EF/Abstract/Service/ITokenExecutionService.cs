using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Service
{
    public interface ITokenExecutionService<TId>
    {
        /// <summary>
        /// Выполнить обработку действий токена.
        /// (Из асинхронного выполнения - процесс AsyncExecuting).
        /// </summary>
        /// <returns></returns>
        ValueTask ExecuteTokenAsync(
            IProcessContainer<TId> process, 
            CancellationToken cancellationToken);

        /// <summary>
        /// Выполнить обработку дейтсвия токена.
        /// (Из внешнего кода - процесс WaitEvent).
        /// </summary>
        /// <returns></returns>
        ValueTask ExecuteActionAsync(
            IProcessContainer<TId> process,
            string actionId,
            CancellationToken cancellationToken,
            string? tokenId = null);
    }
}
