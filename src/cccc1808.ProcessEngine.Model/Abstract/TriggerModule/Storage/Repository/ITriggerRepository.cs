using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository
{
    public interface ITriggerRepository<TId>
    {
        Task<IDictionary<string, ITriggerComponent<TId>>> LoadTriggerForQueueConsumerAsync(
            ICollection<string> keys,
            CancellationToken cancellationToken);

        /// <summary>
        /// Попытаться загрузить триггеры для обработки.
        /// Пытается получить update lock, возвращает только те записи, где он был получен за указанный waitLockTimeout.
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="waitLockTimeout">Время ожидания update lock.</param>
        /// <returns></returns>
        Task<ICollection<ITriggerComponent<TId>>> LoadForHandlerAsync(
            ICollection<TId> ids, 
            TimeSpan waitLockTimeout,
            CancellationToken cancellationToken);

        Task CreateTriggerAsync(
            CreateTriggerDto createDto,
            CancellationToken cancellationToken);

        Task CreateTriggerRangeAsync(
            ICollection<CreateTriggerDto> createDto,
            CancellationToken cancellationToken);

        Task SaveAsync(
            ICollection<ITriggerComponent<TId>> triggers, 
            CancellationToken cancellationToken);

        Task<HashSet<TId>> CheckProcessWaitingAsync(
            ICollection<TId> processIds,
            CancellationToken cancellationToken
            );

        Task<Dictionary<TId, BitFlagDto>> CheckProcessSignalFilterFlagAsync(
            ICollection<TId> processIds,
            CancellationToken cancellationToken);

        public readonly record struct CreateTriggerDto(
            string key,
            DateTimeOffset timerDate,
            TId processId,
            bool isRangeTrigger,
            string handlerKey,
            ITriggerComponent.TriggerKind kind,
            short priority,
            bool isActivated,
            bool? streamProcessIsWaiting,
            long? signalCounter1,
            long? signalCounter2,
            bool isChildTrigger,
            ulong signalCode)
        {
            public static CreateTriggerDto CounterTrigger(
                string key,
                DateTimeOffset timerDate,
                TId processId,
                bool isRangeTrigger,
                string handlerKey,
                short priority,
                bool isActivated,
                int counter,
                bool isChildTrigger,
                ulong? signal = null) => new CreateTriggerDto(
                    key,
                    timerDate,
                    processId,
                    isRangeTrigger,
                    handlerKey, 
                    ITriggerComponent.TriggerKind.Counter,
                    priority, 
                    isActivated, 
                    null,
                    counter,
                    null,
                    isChildTrigger,
                    signal ?? 0);

            public static CreateTriggerDto TimerTrigger(
                string key,
                DateTimeOffset timerDate,
                TId processId,
                bool isRangeTrigger,
                string handlerKey,
                short priority,
                bool isActivated,
                bool isChildTrigger,
                ulong? signal = null) => new CreateTriggerDto(
                    key,
                    timerDate,
                    processId,
                    isRangeTrigger,
                    handlerKey,
                    ITriggerComponent.TriggerKind.Timer,
                    priority,
                    isActivated,
                    null,
                    null,
                    null,
                    isChildTrigger,
                    signal ?? 0);

            public static CreateTriggerDto SimpleStreamTrigger(
                string key,
                DateTimeOffset timerDate,
                TId processId,
                bool isRangeTrigger,
                string handlerKey,
                short priority,
                bool isActivated,
                bool streamProcessIsWaiting,
                long newSignalCounter,
                bool isChildTrigger,
                ulong? signal = null) 
                => new CreateTriggerDto(
                    key,
                    timerDate,
                    processId,
                    isRangeTrigger,
                    handlerKey,
                    ITriggerComponent.TriggerKind.SimpleStream,
                    priority,
                    isActivated,
                    streamProcessIsWaiting,
                    newSignalCounter,
                    null,
                    isChildTrigger,
                    signal ?? 0);

            public static CreateTriggerDto SimpleRootStreamTrigger(
                string key,
                DateTimeOffset timerDate,
                TId processId,
                bool isRangeTrigger,
                string handlerKey,
                short priority,
                bool isActivated,
                bool streamProcessIsWaiting,
                long newSignalCounter)
                => new CreateTriggerDto(
                    key,
                    timerDate,
                    processId,
                    isRangeTrigger,
                    handlerKey,
                    ITriggerComponent.TriggerKind.SimpleStreamRoot,
                    priority,
                    isActivated,
                    streamProcessIsWaiting,
                    newSignalCounter,
                    null,
                    isChildTrigger: false,
                    signalCode: 0);

            public static CreateTriggerDto OffsetStreamTrigger(
                string key,
                DateTimeOffset timerDate,
                TId processId,
                bool isRangeTrigger,
                string handlerKey,
                short priority,
                bool isActivated,
                bool streamProcessIsWaiting,
                long processedOffset,
                long lastOffset,
                bool isChildTrigger,
                ulong? signal = null) => new CreateTriggerDto(
                    key,
                    timerDate,
                    processId,
                    isRangeTrigger,
                    handlerKey,
                    ITriggerComponent.TriggerKind.OffsetStream,
                    priority,
                    isActivated,
                    streamProcessIsWaiting,
                    processedOffset,
                    lastOffset,
                    isChildTrigger,
                    signal ?? 0);
        }
    }
}
