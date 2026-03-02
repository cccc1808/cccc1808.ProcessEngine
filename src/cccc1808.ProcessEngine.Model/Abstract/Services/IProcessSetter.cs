using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.Services
{
    public interface IProcessSetter
    {
        void SetStatus<TId>(
            IProcessContainer<TId> process,
            ProcessStatusEnum status);

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
        void SetError<TId>(
            IProcessContainer<TId> process,
            Exception ex,
            bool allowRetry);

        /// <summary>
        /// Установить таймер асинъронной обработки.
        /// </summary>
        /// <typeparam name="TId"></typeparam>
        /// <param name="process"></param>
        /// <param name="date"></param>
        void SetTimer<TId>(
            IProcessContainer<TId> process,
            DateTimeOffset date);
    }
}
