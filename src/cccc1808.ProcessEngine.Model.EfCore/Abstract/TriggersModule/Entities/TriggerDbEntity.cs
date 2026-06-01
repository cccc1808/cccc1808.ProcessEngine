using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities
{
    public class TriggerDbEntity<TId>
        : IId<TId>, 
        IProcessLinked<TId>
    {
        public TId Id { get; set; }

        /// <summary>
        /// Уникальный ключ триггера.
        /// Можно использовать другой тип (например Guid).
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Используется в том числе для индекса, позволяет меньше конкурировать нодам.
        /// Дополняет updatelock.
        /// Отмечает бронь записи, между select транзакций и транзакций выполнения.
        /// </summary>
        public DateTimeOffset SelectLockTimeout { get; set; }

        /// <summary>
        /// Смещение keyset пагинации.
        /// </summary>
        public TId? OffsetId { get; set; }

        /// <summary>
        /// Таймер выполнения.
        /// </summary>
        public DateTimeOffset TimerDate { get; set; }

        /// <summary>
        /// Является ли хендлер триггера групповым.
        /// (Используется для распределения Task/Transaction).
        /// </summary>
        public bool IsRangeHandler { get; set; }

        /// <summary>
        /// Ключ хендлера.
        /// TODO: можно переделать на число для экономии.
        /// </summary>
        public string HandlerKey { get; set; }

        /// <summary>
        /// Тип триггера.
        /// </summary>
        public ITriggerComponent.TriggerKind Kind { get; set; }

        /// <summary>
        /// Приоритет.
        /// </summary>
        public short Priority { get; set; }

        /// <summary>
        /// Триггер активирован (требуется обработка).
        /// Еще влияет <see cref="TimerDate"/>.
        /// </summary>
        public bool IsActivated { get; set; }

        /// <summary>
        /// Триггер завершен. Не обрабатывается, не принимает сигналы.
        /// </summary>
        public bool IsCompleted { get; set; }

        public TId ProcessId { get; set; }

        public bool? StreamProcessIsWaiting { get; set; }

        public long? SignalCounter1 { get; set; }

        public long? SignalCounter2 { get; set; }

        /// <summary>
        /// Замечание: можно хранить кастомное состояние в json/bin (StreamProcessIsWaiting, SignalCounter1, SignalCounter2).
        /// Это позволит более просто добавлять новые типы триггеров.
        /// Но пока список типов триггеров фиксированный, все поля лежат в строке.
        /// </summary>
        //// public JsonElement State { get; set; }

        [Obsolete("For entity framework")]
        public TriggerDbEntity() 
        {
            Id = default!;
            Key = default!;
            ProcessId = default!;
            HandlerKey = default!;
        }

        public TriggerDbEntity(
            TId id,
            string key,
            DateTimeOffset selectLockTimeout,
            DateTimeOffset timerDate,
            bool isRangeHandler,
            string handlerKey,
            ITriggerComponent.TriggerKind kind,
            short priority,
            bool isActivated,
            bool isCompleted,
            TId processId,
            bool? streamProcessIsWaiting,
            long? signalCounter1,
            long? signalCounter2)
        {
            Id = id;
            Key = key;
            SelectLockTimeout = selectLockTimeout;
            TimerDate = timerDate;
            IsRangeHandler = isRangeHandler;
            HandlerKey = handlerKey;
            Kind = kind;
            Priority = priority;
            IsActivated = isActivated;
            IsCompleted = isCompleted;
            ProcessId = processId;

            switch (kind)
            {
                case ITriggerComponent.TriggerKind.Counter:
                    {
                        SignalCounter1 = signalCounter1.Value;
                        break;
                    }

                case ITriggerComponent.TriggerKind.SimpleStream:
                case ITriggerComponent.TriggerKind.SimpleStreamRoot:
                {
                        StreamProcessIsWaiting = streamProcessIsWaiting.Value;
                        SignalCounter1 = signalCounter1.Value;
                        break;
                    }

                case ITriggerComponent.TriggerKind.OffsetStream:
                    {
                        StreamProcessIsWaiting = streamProcessIsWaiting.Value;
                        SignalCounter1 = signalCounter1.Value;
                        SignalCounter2 = signalCounter2.Value;
                        break;
                    }

                case ITriggerComponent.TriggerKind.Timer:                
                    break;

                default: throw new NotImplementedException($"{Kind}.");
            }            
        }
    }
}
