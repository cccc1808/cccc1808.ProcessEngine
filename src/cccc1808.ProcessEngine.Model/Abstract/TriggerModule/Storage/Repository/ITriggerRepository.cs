using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;

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
        
        public readonly record struct CreateTriggerDto(
            string key,
            DateTimeOffset timerDate,
            TId processId,
            string handlerKey,
            ITriggerComponent.TriggerKind kind,
            short priority,
            bool isActivated,
            bool? streamProcessIsWaiting,
            long? signalCounter1,
            long? signalCounter2)
        {
            public static CreateTriggerDto CounterTrigger(
                string key,
                DateTimeOffset timerDate,
                TId processId,
                string handlerKey,
                short priority,
                bool isActivated,
                int counter) => new CreateTriggerDto(
                    key,
                    timerDate,
                    processId,
                    handlerKey, 
                    ITriggerComponent.TriggerKind.Counter,
                    priority, 
                    isActivated, 
                    null,
                    counter,
                    null);

            public static CreateTriggerDto TimerTrigger(
                string key,
                DateTimeOffset timerDate,
                TId processId,
                string handlerKey,
                short priority,
                bool isActivated) => new CreateTriggerDto(
                    key,
                    timerDate,
                    processId,
                    handlerKey,
                    ITriggerComponent.TriggerKind.Timer,
                    priority,
                    isActivated,
                    null,
                    null,
                    null);

            public static CreateTriggerDto SimpleStreamTrigger(
                string key,
                DateTimeOffset timerDate,
                TId processId,
                string handlerKey,
                short priority,
                bool isActivated,
                bool streamProcessIsWaiting,
                long newSignalCounter) => new CreateTriggerDto(
                    key,
                    timerDate,
                    processId,
                    handlerKey,
                    ITriggerComponent.TriggerKind.SimpleStream,
                    priority,
                    isActivated,
                    streamProcessIsWaiting,
                    newSignalCounter,
                    null);

            public static CreateTriggerDto OffsetStreamTrigger(
                string key,
                DateTimeOffset timerDate,
                TId processId,
                string handlerKey,
                short priority,
                bool isActivated,
                bool streamProcessIsWaiting,
                long processedOffset,
                long lastOffset) => new CreateTriggerDto(
                    key,
                    timerDate,
                    processId,
                    handlerKey,
                    ITriggerComponent.TriggerKind.OffsetStream,
                    priority,
                    isActivated,
                    streamProcessIsWaiting,
                    processedOffset,
                    lastOffset);
        }
    }
}
