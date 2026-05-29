using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services
{
    /// <summary>
    /// https://wiki.denhome.ru/bin/view/Проекты%20и%20репозитории/Библиотеки/Движок%20cccc1808.%20ProcessEngine/Про%20передачу%20сигнала%20на%20процесс/
    /// Типы передачи сигналов 2.2.1
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IRootTriggerService<TId>
    {
        /// <summary>
        /// Передать информацию о сигнале от дочерних триггеров на корневой триггер процесса.
        /// </summary>
        /// <param name="triggers"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task SignalToRootTriggerAsync(
            ICollection<ITriggerComponent<TId>> triggers, 
            CancellationToken cancellationToken);

        /// <summary>
        /// Передать информацию на дочерние триггеры о том, что корневой триггер выполнился.
        /// </summary>
        /// <param name="rootTriggers"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task RootTriggerProcessGoSleepAsync(
            ICollection<ITriggerComponent<TId>> rootTriggers,
            CancellationToken cancellationToken);

        public interface IQueries
        {
            /// <summary>
            /// Выбор идентефикаторов дочерних триггеров, которые нужно оповестить, 
            /// что корневой триггер выполнился.
            /// </summary>
            /// <param name="rootTriggers"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            Task<ICollection<(TId ProcessId, string Key)>> GetChildTriggersForRootTriggerProcessGoSleepAsyncAsync(
                ICollection<ITriggerComponent<TId>> rootTriggers,            
                CancellationToken cancellationToken);
        }
    }
}
