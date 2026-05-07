using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Components
{
    public class EFTriggerProxyComponent<TId>
        : ITriggerComponent<TId>
    {
        private readonly ITriggerSetter<TId> _triggerSetter;
        private readonly TriggerDbEntity<TId> _entity;        

        public string Key => _entity.Key;

        public TId ProcessId => _entity.ProcessId;

        public bool IsActivated { get => _entity.IsActivated; set => _entity.IsActivated = value; }

        public bool IsCompleted { get => _entity.IsCompleted; set => _entity.IsCompleted = value; }

        public DateTimeOffset TimerDate { get => _entity.TimerDate; set => _entity.TimerDate = value; }

        public string HandlerKey => _entity.HandlerKey;

        public ITriggerComponent.TriggerKind Kind => _entity.Kind;
        
        public DateTimeOffset SelectLockTimeout { get => _entity.SelectLockTimeout; set => _entity.SelectLockTimeout = value; }

        public object? State { get; private set; }        

        public EFTriggerProxyComponent(
            ITriggerSetter<TId> triggerSetter, 
            TriggerDbEntity<TId> entity)
        {
            _triggerSetter = triggerSetter;
            _entity = entity;

            _triggerSetter.OneOfTriggerKind(
                Kind,
                this,
                counterHandler: static (p) => 
                {
                    p.State = new EFCounterProxyDto(p._entity);
                },
                timerHandler: static (_) => { },
                simpleStreamHandler: static (p) => 
                {
                    p.State = new EFSimpleStreamProxyDto(p._entity);
                },
                offsetStreamHanler: static (p) => 
                {
                    p.State = new EFOffsetStreamProxyDto(p._entity);
                });
        }


        private class EFCounterProxyDto 
            : ITriggerComponent.ICounterDto
        {
            private readonly TriggerDbEntity<TId> _entity;

            public long Counter { get => _entity.SignalCounter1.Value; set => _entity.SignalCounter1 = value; }

            public EFCounterProxyDto(TriggerDbEntity<TId> entity)
            {
                _entity = entity;
            }
        }

        private class EFSimpleStreamProxyDto : ITriggerComponent.ISimpleStreamDto
        {
            private readonly TriggerDbEntity<TId> _entity;

            public bool StreamsProcessIsWaiting { get => _entity.StreamProcessIsWaiting.Value; set => _entity.StreamProcessIsWaiting = value; }

            public long NewSignalCounter { get => _entity.SignalCounter1.Value; set => _entity.SignalCounter1 = value; }

            public EFSimpleStreamProxyDto(TriggerDbEntity<TId> entity)
            {
                _entity = entity;
            }
        }

        private class EFOffsetStreamProxyDto : ITriggerComponent.IOffsetStreamDto
        {
            private readonly TriggerDbEntity<TId> _entity;

            public bool StreamsProcessIsWaiting { get => _entity.StreamProcessIsWaiting.Value; set => _entity.StreamProcessIsWaiting = value; }

            public long LastOffset { get => _entity.SignalCounter2.Value; set => _entity.SignalCounter2 = value; }

            public long ProcessedOffset { get => _entity.SignalCounter1.Value; set => _entity.SignalCounter1 = value; }            

            public EFOffsetStreamProxyDto(TriggerDbEntity<TId> entity)
            {
                _entity = entity;
            }
        }
    }
}
