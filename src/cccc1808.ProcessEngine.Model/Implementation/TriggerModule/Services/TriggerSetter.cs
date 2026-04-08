using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services
{
    public class TriggerSetter<TId> : ITriggerSetter<TId>
    {
        public bool IsCounterActivated(ITriggerComponent<TId> trigger)
        {
            return trigger.Counter.Value <= 0;
        }

        public void ProcessCounter(ITriggerComponent<TId> trigger, int eventCount)
        {
            trigger.Counter -= eventCount;
        }

        public void SetActivated(ITriggerComponent<TId> trigger, bool value)
        {
            trigger.IsActivated = value;
        }

        public void SetStreamsState(
            ITriggerComponent<TId> trigger, 
            bool processIsWaiting,
            Dictionary<string, long> channels,
            Dictionary<string, long> process)
        {
            trigger.StreamsProcessIsWaiting = processIsWaiting;
            trigger.StreamsTimeStamp = channels;
            trigger.StreamProcessTimestamps = process;
        }

        public void SetCompleted(ITriggerComponent<TId> trigger, bool value)
        {
            trigger.IsCompleted = value;
        }

        public void SetTimer(ITriggerComponent<TId> trigger, DateTimeOffset value)
        {
            trigger.TimerDate = value;
        }

        public void OneOf(
            ITriggerComponent<TId> trigger,
            Action<int> counterHandler, 
            Action timerHandler, 
            Action<bool, IReadOnlyDictionary<string, long>, IReadOnlyDictionary<string, long>> streamHandler
            )
        {
            {
                switch (trigger.Kind)
                {
                    case ITriggerComponent<TId>.TriggerKind.Counter:
                        {
                            counterHandler(trigger.Counter!.Value);
                            break;
                        }

                    case ITriggerComponent<TId>.TriggerKind.Timer:
                        {
                            timerHandler();
                            break;
                        }

                    case ITriggerComponent<TId>.TriggerKind.StreamsTrigger:
                        {
                            streamHandler(trigger.StreamsProcessIsWaiting.Value, trigger.StreamsTimeStamp, trigger.StreamProcessTimestamps);
                            break;
                        }

                    default: throw new NotImplementedException("[Bug]");
                }
            }
        }

        public async ValueTask OneOfAsync(
            ITriggerComponent<TId> trigger,
            Func<int, ValueTask> counterHandler,
            Func<ValueTask> timerHandler,
            Func<bool, IReadOnlyDictionary<string, long>, IReadOnlyDictionary<string, long>, ValueTask> streamHandler)
        {
            {
                switch (trigger.Kind)
                {
                    case ITriggerComponent<TId>.TriggerKind.Counter:
                        {
                            await counterHandler(trigger.Counter!.Value);
                            break;
                        }

                    case ITriggerComponent<TId>.TriggerKind.Timer:
                        {
                            await timerHandler();
                            break;
                        }

                    case ITriggerComponent<TId>.TriggerKind.StreamsTrigger:
                        {
                            streamHandler(trigger.StreamsProcessIsWaiting.Value, trigger.StreamsTimeStamp, trigger.StreamProcessTimestamps);
                            break;
                        }

                    default: throw new NotImplementedException("[Bug]");
                }
            }
        }
    }
}
