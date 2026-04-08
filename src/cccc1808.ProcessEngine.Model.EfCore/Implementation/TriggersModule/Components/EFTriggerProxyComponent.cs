using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
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

        public DateTimeOffset SelectLockTimeout { get => _entity.SelectLockTimeout; set => _entity.SelectLockTimeout = value; }

        public JsonElement? StreamData 
        { 
            get 
            {
                if (Kind == ITriggerComponent<TId>.TriggerKind.StreamsTrigger)
                {
                    using (var document = JsonSerializer.SerializeToDocument(
                        new StreamJsonDto()
                        {
                            StreamsTimeStamp = StreamsTimeStamp,
                            StreamProcessTimestamps = StreamProcessTimestamps,
                            StreamsProcessIsWaiting = StreamsProcessIsWaiting.Value
                        }))
                    {
                        return document.RootElement.Clone();
                    }
                }

                return null;                
            }
            set 
            {
                if (value.HasValue)
                {
                    var state = JsonSerializer.Deserialize<StreamJsonDto>(value.Value);
                    StreamsProcessIsWaiting = state.StreamsProcessIsWaiting;
                    StreamsTimeStamp = state.StreamsTimeStamp;
                    StreamProcessTimestamps = state.StreamProcessTimestamps;
                }
            }
        }

        public bool? StreamsProcessIsWaiting { get; set; }

        public Dictionary<string, long>? StreamsTimeStamp { get; set; }

        public Dictionary<string, long>? StreamProcessTimestamps { get; set; }


        public class StreamJsonDto 
        {
            public bool StreamsProcessIsWaiting { get; set; }

            public Dictionary<string, long> StreamsTimeStamp { get; set; } = default!;

            public Dictionary<string, long> StreamProcessTimestamps { get; set; } = default!;
        }
    }
}
