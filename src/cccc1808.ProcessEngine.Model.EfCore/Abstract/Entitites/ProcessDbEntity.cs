using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Common.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites
{
    /// <summary>
    /// Легковестная таблица, содержащая набор, необходимый для распределения процессов.
    /// Другие данные процесса хранить в отдельной таблице.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class ProcessDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }

        public long ProcessTypeId { get; set; }
        public int ProcessVersion { get; set; }
        public short Priority { get; set; }

        public DateTimeOffset SelectLock { get; set; }

        #region Status

        /// <summary>
        /// Процесс содержит ошибку.
        /// </summary>
        public bool HaveErrorFlag { get; set; }

        public ProcessStatusEnum Status { get; set; }

        #endregion

        #region Error        

        public short? ReTryCount { get; set; }

        public ProcessErrorDbEntity<TId> Error { get; set; }

        #endregion

        #region Timer

        public TId? TimerLinkedProcessId { get; set; }
        public ProcessDbEntity<TId>? LinkedProcess { get; set; }
        public DateTimeOffset TimerDate { get; set; }

        #endregion
    }
}
