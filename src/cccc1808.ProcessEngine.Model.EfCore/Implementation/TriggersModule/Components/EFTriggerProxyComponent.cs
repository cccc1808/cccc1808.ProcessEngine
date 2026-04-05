using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Components
{
    public class EFTriggerProxyComponent<TId>
        : ITriggerComponent<TId>
    {
        private readonly TriggerDbEntity<TId> _entity;

        public EFTriggerProxyComponent(TriggerDbEntity<TId> entity)
        {
            _entity = entity;
        }

        public int? Counter { get => _entity.Counter; set => _entity.Counter = value; }

        public string Key => _entity.Key;

        public TId ProcessId => _entity.ProcessId;

        public bool IsActivated { get => _entity.IsActivated; set => _entity.IsActivated = value; }

        public bool IsCompleted { get => _entity.IsCompleted; set => _entity.IsCompleted = value; }

        public DateTimeOffset TimerDate { get => _entity.TimerDate; set => _entity.TimerDate = value; }

        public string HandlerKey => _entity.HandlerKey;

        public ITriggerComponent<TId>.TriggerKind Kind => _entity.Kind;

        public DateTimeOffset SelectTimer { get => _entity.SelectTimer; set => _entity.SelectTimer = value; }
    }
}
