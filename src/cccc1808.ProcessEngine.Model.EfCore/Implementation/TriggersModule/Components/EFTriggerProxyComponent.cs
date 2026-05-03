using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Components
{
    public class EFTriggerProxyComponent<TId>
        : ITriggerComponent<TId>
    {
        private readonly ITriggerSetter<TId> _triggerSetter;
        private readonly TriggerDbEntity<TId> _entity;        

        public int? Counter { get => _entity.Counter; set => _entity.Counter = value; }

        public string Key => _entity.Key;

        public TId ProcessId => _entity.ProcessId;

        public bool IsActivated { get => _entity.IsActivated; set => _entity.IsActivated = value; }

        public bool IsCompleted { get => _entity.IsCompleted; set => _entity.IsCompleted = value; }

        public DateTimeOffset TimerDate { get => _entity.TimerDate; set => _entity.TimerDate = value; }

        public string HandlerKey => _entity.HandlerKey;

        public ITriggerComponent<TId>.TriggerKind Kind => _entity.Kind;

        public DateTimeOffset SelectLockTimeout { get => _entity.SelectLockTimeout; set => _entity.SelectLockTimeout = value; }
        
        public ITriggerComponent<TId>.ISimpleStreamDto? SimpleStreamState { get; private set; }

        public ITriggerComponent<TId>.IOffsetStreamDto? OffsetStreamState { get; private set; }

        public EFTriggerProxyComponent(
            ITriggerSetter<TId> triggerSetter, 
            TriggerDbEntity<TId> entity)
        {
            _triggerSetter = triggerSetter;
            _entity = entity;

            _triggerSetter.OneOf(
                Kind,
                counterHandler: () => { },
                timerHandler: () => { },
                simpleStreamHandler: () => 
                {
                    SimpleStreamState = new DefaultTriggerComponent.SimpleStreamDto<TId>(
                        _entity.SimpleStreamState.StreamsProcessIsWaiting,
                        _entity.SimpleStreamState.NewSignalCounter);
                },
                offsetStreamHanler: () => 
                {
                    OffsetStreamState = new DefaultTriggerComponent.OffsetStreamDto<TId>(
                        _entity.OffsetStreamState.StreamsProcessIsWaiting,
                        _entity.OffsetStreamState.ChannelsOffsets.ToDictionary(
                            e => e.Key,
                            e => (ITriggerComponent<TId>.IOffsetStreamDto.IEntryDto)new DefaultTriggerComponent.OffsetStreamDto<TId>.EntryDto(e.Value.LastOffset, e.Value.ProcessedOffset)));
                });
        }

        public void StreamStateChanged()
        {
            _triggerSetter.OneOf(
                this,
                counterHandler: (_) => { },
                timerHandler: () => { },
                simpleStreamHandler: (state) =>
                {
                    _entity.SimpleStreamState.StreamsProcessIsWaiting = state.StreamsProcessIsWaiting;
                    _entity.SimpleStreamState.NewSignalCounter = state.NewSignalCounter;
                },
                offsetStreamHanler: (state) =>
                {
                    _entity.OffsetStreamState.StreamsProcessIsWaiting = state.StreamsProcessIsWaiting;
                    _entity.OffsetStreamState.ChannelsOffsets = state.ChannelsOffsets.ToDictionary(
                        e => e.Key,
                        e => new TriggerDbEntity<TId>.OffsetStreamDto.OffsetEntry(e.Value.LastOffset, e.Value.ProcessedOffset));
                });
        }
    }
}
