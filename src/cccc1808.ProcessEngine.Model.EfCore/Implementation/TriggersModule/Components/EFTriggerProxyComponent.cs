using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Components
{
    public class EFTriggerProxyComponent<TId>
        : ITriggerComponent<TId>
    {
        private readonly ITriggerSetter<TId> _triggerSetter;
        public TriggerDbEntity<TId> Entity { get; }       

        public string Key => Entity.Key;

        public TId ProcessId => Entity.ProcessId;

        public bool IsActivated { get => Entity.IsActivated; set => Entity.IsActivated = value; }

        public bool IsCompleted { get => Entity.IsCompleted; set => Entity.IsCompleted = value; }

        public DateTimeOffset TimerDate { get => Entity.TimerDate; set => Entity.TimerDate = value; }

        public string HandlerKey => Entity.HandlerKey;

        public ITriggerComponent.TriggerKind Kind => Entity.Kind;
        
        public DateTimeOffset SelectLockTimeout { get => Entity.SelectLockTimeout; set => Entity.SelectLockTimeout = value; }

        public object? State { get; private set; }

        public TId? OffsetId { get => Entity.OffsetId; set => Entity.OffsetId = value; }

        public bool NeedUpdate { get; set; }

        public bool NeedRemove { get; set; }

        public ITriggerComponent.IChildTriggerDto? ChildTrigger { get; private set; }

        public EFTriggerProxyComponent(
            ITriggerSetter<TId> triggerSetter, 
            TriggerDbEntity<TId> entity)
        {
            _triggerSetter = triggerSetter;
            Entity = entity;

            _triggerSetter.OneOfTriggerSetter.OneOfKind(
                Kind,
                this,
                counterHandler: static (p) => 
                {
                    p.State = new EFCounterProxyDto(p.Entity);
                },
                timerHandler: static (_) => { },
                simpleStreamHandler: static (p) => 
                {
                    p.State = new EFSimpleStreamProxyDto(p.Entity);
                },
                offsetStreamHanler: static (p) => 
                {
                    p.State = new EFOffsetStreamProxyDto(p.Entity);
                });

            ChildTrigger = entity.ChildTrigger_CompleteAfterDelivery.HasValue 
                ? new EFChildTriggerProxyDto(entity) 
                : null;
        }


        private class EFChildTriggerProxyDto 
            : ITriggerComponent.IChildTriggerDto
        {           
            private TriggerDbEntity<TId> Entity { get; }

            public bool CompleteAfterDelivery { get => Entity.ChildTrigger_CompleteAfterDelivery!.Value; set => Entity.ChildTrigger_RemoveAftrerDelivery = value; }

            public bool RemoveAftrerDelivery { get => Entity.ChildTrigger_RemoveAftrerDelivery!.Value; set => Entity.ChildTrigger_RemoveAftrerDelivery = value; }

            public long? WaitDeliveryTimestamp { get => Entity.ChildTrigger_WaitDeliveryTimestamp; set => Entity.ChildTrigger_WaitDeliveryTimestamp = value; }

            public EFChildTriggerProxyDto(
                TriggerDbEntity<TId> entity)
            {
                if (!entity.ChildTrigger_CompleteAfterDelivery.HasValue)
                {
                    throw new ArgumentException(nameof(entity));
                }
                if (!entity.ChildTrigger_RemoveAftrerDelivery.HasValue)
                {
                    throw new ArgumentException(nameof(entity));
                }

                Entity = entity;
            }
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

            public bool IsRootTrigger { get => _entity.Kind == ITriggerComponent.TriggerKind.SimpleStreamRoot; }

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
