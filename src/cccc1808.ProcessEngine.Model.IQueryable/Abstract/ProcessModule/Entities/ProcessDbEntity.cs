using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities
{
    /// <summary>
    /// Легковестная таблица, содержащая набор, необходимый для распределения процессов.
    /// Другие данные процесса хранить в отдельной таблице.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class ProcessDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; } = default!;

        public long ProcessTypeId { get; set; }
        public int ProcessVersion { get; set; }
        public short Priority { get; set; }

        /// <summary>
        /// Используется в том числе для индекса, позволяет меньше конкурировать нодам.
        /// Дополняет updatelock.
        /// </summary>
        public DateTimeOffset SelectLockTimeout { get; set; }

        #region Status

        /// <summary>
        /// Процесс содержит ошибку.
        /// </summary>
        public bool StoppedByError { get; set; }

        public ProcessStatusEnum Status { get; set; }

        #endregion

        #region Error        

        public short? RetryCount { get; set; }

        public ProcessErrorDbEntity<TId>? Error { get; set; } = default!;

        #endregion

        public ProcessDbEntity() { }

        public ProcessDbEntity(
            TId id, 
            long processTypeId, 
            int processVersion, 
            short priority, 
            DateTimeOffset selectLockTimeout, 
            bool stoppedByError, 
            ProcessStatusEnum status, 
            short? retryCount)
        {
            Id = id;
            ProcessTypeId = processTypeId;
            ProcessVersion = processVersion;
            Priority = priority;
            SelectLockTimeout = selectLockTimeout;
            StoppedByError = stoppedByError;
            Status = status;
            RetryCount = retryCount;
        }
    }
}
