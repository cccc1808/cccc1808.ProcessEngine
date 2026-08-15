using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services
{
    /// <summary>
    /// Часто используемые методы для .
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface ITriggerHandlerFacade<TId>
    {
        /// <summary>
        /// Проверка и блокировка процесса при использовании NoWakeup пробуждения.
        /// Если процесс в <see cref="ProcessStatusEnum.WaitEvent"/>, то пытается взять блокировку.
        /// Если блокировка взята - то можно обрабатывать, иначе нельзя.
        /// Если процесс завершен или удален, то обрабокта не требуется и скорее всего тригер нужно удалить.
        /// </summary>
        /// <param name="triggers"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<LockForWaitProcessResult> LockForWaitProcessAsync(
            IEnumerable<ITriggerComponent<TId>> triggers,
            CancellationToken cancellationToken);

        /// <summary>
        /// Проверка состояния процесса для wakeup пробуждения.
        /// Предпологается при срабатывании emergency триггера.
        /// Проверить, что процесс не завершен и не удален.
        /// </summary>
        /// <param name="triggers"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<CheckCompleteOrNotFoundResult> CheckCompleteOrNotFound(
            IEnumerable<ITriggerComponent<TId>> triggers,
            CancellationToken cancellationToken);

        /// <summary>
        /// Выполнить пробуждения процесса напрямую (без wakaup state).
        /// Треюует предворительного получения update lock.
        /// </summary>
        Task ToAsyncExecutingNoWakeupAsync(
            ICollection<TId> processIds, 
            CancellationToken cancellationToken);

        /// <param name="WaitWithLock">Процесс в состоянии ожидания и получен update lock (можно пробуждать).</param>
        /// <param name="WaitWithoutLock">Процесс в состоянии ожидания, но блокировка не получена (скорее всего timeout).</param>
        /// <param name="IsAsyncExecuting">Процесс в состоянии асинхронной обработки (пробуждение не требуется).</param>
        /// <param name="InComplete">Процесс завершен (триггер можно удалять).</param>
        /// <param name="NotFound">Процесс не найден (триггер можно удалять).</param>
        public readonly record struct LockForWaitProcessResult(
            ICollection<ITriggerComponent<TId>> WaitWithLock,
            ICollection<ITriggerComponent<TId>> WaitWithoutLock,
            ICollection<ITriggerComponent<TId>> IsAsyncExecuting,
            ICollection<ITriggerComponent<TId>> InComplete,
            ICollection<ITriggerComponent<TId>> NotFound
            );

        /// <param name="InComplete">Процесс завершен (триггер можно удалять).</param>
        /// <param name="NotFound">Процесс не найден (триггер можно удалять).</param>
        /// <param name="Other">Процесс не завершен (можно пробуждать)</param>
        public readonly record struct CheckCompleteOrNotFoundResult(
            ICollection<ITriggerComponent<TId>> InComplete,
            ICollection<ITriggerComponent<TId>> NotFound,
            ICollection<ITriggerComponent<TId>> Other
            );

        /// <param name="Queue">Очередь для отправки TriggerEvent на корневой процесс.</param>
        /// <param name="RootTriggerKey">Ключ корневогг триггера.</param>
        public readonly record struct RootEventInfoDto(
            string Queue,
            string RootTriggerKey
            );
    }
}
