using System;
using System.Collections.Generic;
using System.Linq;
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

        public string Key { get; set; }

        /// <summary>
        /// Используется в том числе для индекса, позволяет меньше конкурировать нодам.
        /// Дополняет updatelock.
        /// </summary>
        public DateTimeOffset SelectLockTimeout { get; set; }

        public DateTimeOffset TimerDate { get; set; }

        /// <summary>
        /// TODO: можно переделать на число для экономии.
        /// </summary>
        public string HandlerKey { get; set; }

        public ITriggerComponent<TId>.TriggerKind Kind { get; set; }

        public short Priority { get; set; }

        public bool IsActivated { get; set; }

        public bool IsCompleted { get; set; }

        public TId ProcessId { get; set; }

        public int? Counter { get; set; }

        public TriggerDbEntity(
            TId id, 
            string key, 
            DateTimeOffset selectLockTimeout, 
            DateTimeOffset timerDate,
            string handlerKey,
            ITriggerComponent<TId>.TriggerKind kind,
            short priority, 
            bool isActivated, 
            bool isCompleted,
            TId processId, 
            int? counter)
        {
            Id = id;
            Key = key;
            SelectLockTimeout = selectLockTimeout;
            TimerDate = timerDate;
            HandlerKey = handlerKey;
            Kind = kind;
            Priority = priority;
            IsActivated = isActivated;
            IsCompleted = isCompleted;
            ProcessId = processId;
            Counter = counter;
        }
    }
}
