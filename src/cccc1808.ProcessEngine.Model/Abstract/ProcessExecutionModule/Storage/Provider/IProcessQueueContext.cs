using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Storage.Provider
{
    public interface IProcessQueueContext<TId>
    {
        void IncreseBufferCapacity(int value);

        void SetReserveTimeout(TimeSpan reserveTimeout);

        /// <summary>
        /// * Процесс был создан.
        /// * Процесс перешел в состояние ассинхронного выполнения.
        /// </summary>
        /// <param name="id">id.</param>
        /// <param name="efEntity">Entity, если используется ChangeTracker.</param>
        void ProcessToExecute(ProcessDto process);

        /// <summary>
        /// Обработка процесса сработала, но процесс продолжает выполняться.
        /// Продление резерва и помещение в очередь.
        /// </summary>
        /// <param name="id"></param>
        void ProcessContinueExecute(ProcessDto process);

        /// <summary>
        /// Процесс был обработан и сейчас не выполняется.
        /// Снимает резерв.
        /// </summary>
        /// <param name="id"></param>
        void ProcessExecuted(TId id);        

        /// <summary>
        /// DbSelector выбрал процессы для запуска на выполнение.
        /// Проверяет резервирование и запускает процессы.
        /// </summary>
        /// <returns>Есть ли в очереди еще места.</returns>
        Task<bool> ProcessFromSelectorAsync(
            ICollection<ProcessDto> ids,
            DateTimeOffset reserveDate,
            CancellationToken cancellationToken);

        public readonly record struct ProcessDto(
            TId? Id,
            IId<TId>? EfEntity,
            ProcessRegistryDto ProcessRegistry)
        {
            public TId GetId() => EfEntity is null
                ? Id ?? throw new Exception()
                : EfEntity.Id;

            public static ProcessDto ProcessToExecute(
                TId Id,
                ProcessRegistryDto ProcessRegistry) => new ProcessDto(Id, null, ProcessRegistry);

            public static ProcessDto TriggerToExecute(
                IId<TId> EfEntity,
                ProcessRegistryDto ProcessRegistry) => new ProcessDto(default, EfEntity, ProcessRegistry);

            public static ProcessDto TriggerContinueRun(
                TId Id,
                ProcessRegistryDto ProcessRegistry) => new ProcessDto(Id, null, ProcessRegistry);

            //public static TriggerDto TriggerExecuted(
            //    TId Id,
            //    bool IsRangeTrigger,
            //    string HandlerName) => new TriggerDto(Id, null, IsRangeTrigger, HandlerName);

            public static ProcessDto ProcessFromSelector(
                TId Id,
                ProcessRegistryDto ProcessRegistry) => new ProcessDto(Id, null, ProcessRegistry);
        }
    }
}
