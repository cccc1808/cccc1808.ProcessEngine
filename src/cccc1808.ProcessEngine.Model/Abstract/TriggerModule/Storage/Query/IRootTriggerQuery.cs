using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query
{
    /// <summary>
    /// TODO: Вопрос реализаци:
    /// * Можно хранить root key в самом триггере (тогда этот компонент не нужен).
    /// * Можно сделать отдельную таблица привязки processId - rootTriggerKey, тогда реализация бужет общая (но нужно заполнять и отчищать значения).
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IRootTriggerQuery<TId>
    {
        ValueTask<string?> GetRootTriggerKeyAsync(
            ITriggerComponent<TId> triggerComponent, 
            CancellationToken cancellationToken);
    }
}
