using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services
{
    public interface IProcessSetter
    {
        void SetStatus<TId>(
            IProcessContainer<TId> process,
            ProcessStatusEnum status);

        /// <summary>
        /// Остановить обработку в текущей сессии.
        /// </summary>
        /// <typeparam name="TId"></typeparam>
        /// <param name="process"></param>
        /// <param name="value"></param>
        void StopAsyncProcessingSession<TId>(
            IProcessContainer<TId> process,
            bool value = true);

        /// <summary>
        /// Сбросить состояние ошибки.
        /// </summary>
        /// <typeparam name="TId"></typeparam>
        /// <param name="process"></param>
        void ClearError<TId>(
            IProcessContainer<TId> process);

        /// <summary>
        /// Сохранить ошибку.
        /// </summary>
        /// <typeparam name="TId"></typeparam>
        /// <param name="process">Процесс.</param>
        /// <param name="ex">Ошибка.</param>
        /// <param name="allowRetry">Допустимость задействования ReTry.</param>
        (bool IsRetry, DateTimeOffset Timeout) SetError<TId>(
            IProcessContainer<TId> process,
            Exception ex,
            bool allowRetry);
    }
}
