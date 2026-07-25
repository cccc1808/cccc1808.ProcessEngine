using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Service
{
    public interface ITriggerStateService<TId>
    {
        /// <summary>
        /// Удаление триггеры по признаку движения токена.
        /// </summary>
        ValueTask RemoveTriggersMoveToken(
            IProcessContainer<TId> process,
            string tokenId,
            CancellationToken cancellationToken);

        /// <summary>
        /// Удалить триггеры по признаку завершения действия.
        /// </summary>
        ValueTask RemoveTriggerActionCompleteAsync(
            IProcessContainer<TId> process, 
            string actionId,
            CancellationToken cancellationToken);

        /// <summary>
        /// Удалить триггеры по признаку завершения процесса.
        /// </summary>
        ValueTask RemoveTriggersProcessCompleteAsync(
            IProcessContainer<TId> process,
            CancellationToken cancellationToken);
    }
}
