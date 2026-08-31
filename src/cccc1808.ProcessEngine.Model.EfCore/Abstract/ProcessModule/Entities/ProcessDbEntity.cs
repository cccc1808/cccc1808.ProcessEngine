using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Query;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities
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
        /// Используется в <see cref="EFParallelLimitProcessSelectQuery{TId, TEntity}"/> для распределения между слотами параллельного выполнения.
        /// TODO: не учитывается в индексах.
        /// </summary>
        public bool IsRangeExecution { get; set; }

        /// <summary>
        /// * Указывает нодам сервиса, что процес уже зарезирвирован нодой на выполнение (даже если на нем нет Updatelock).
        /// * Индексируется (в отличии от updatelock).
        /// </summary>
        public DateTimeOffset ReservationTimeout { get; set; }

        /// <summary>
        /// Поступившие сигналы.
        /// </summary>
        public ulong SignalCode { get; set; }

        /// <summary>
        /// Фильтр сигналов: не игнорируемые сигналы.
        /// </summary>
        public ulong SignalCodeFilter { get; set; }

        #region Status

        /// <summary>
        /// Процесс содержит ошибку.
        /// </summary>
        public bool StoppedByError { get; set; }

        public ProcessStatusEnum Status { get; set; }

        #endregion

        #region Error        

        public short? RetryCount { get; set; }

        public ProcessErrorDbEntity<TId> Error { get; set; } = default!;

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
            ReservationTimeout = selectLockTimeout;
            StoppedByError = stoppedByError;
            Status = status;
            RetryCount = retryCount;
        }
    }
}
