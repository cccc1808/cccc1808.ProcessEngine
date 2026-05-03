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

        public void SetCompleted(ITriggerComponent<TId> trigger, bool value)
        {
            trigger.IsCompleted = value;
        }

        public void SetTimer(ITriggerComponent<TId> trigger, DateTimeOffset value)
        {
            trigger.TimerDate = value;
        }

        public void OneOf(
            ITriggerComponent<TId>.TriggerKind kind,
            Action counterHandler,
            Action timerHandler,
            Action simpleStreamHandler,
            Action offsetStreamHanler)
        {
            switch (kind)
            {
                case ITriggerComponent<TId>.TriggerKind.Counter:
                    {
                        counterHandler();
                        break;
                    }

                case ITriggerComponent<TId>.TriggerKind.Timer:
                    {
                        timerHandler();
                        break;
                    }

                case ITriggerComponent<TId>.TriggerKind.SimpleStream:
                    {
                        simpleStreamHandler();
                        break;
                    }

                case ITriggerComponent<TId>.TriggerKind.OffsetStream:
                    {
                        offsetStreamHanler();
                        break;
                    }

                default: throw new NotImplementedException("[Bug]");
            }
        }

        public void OneOf(
            ITriggerComponent<TId> trigger, 
            Action<int> counterHandler,
            Action timerHandler, 
            Action<ITriggerComponent<TId>.ISimpleStreamDto> simpleStreamHandler,
            Action<ITriggerComponent<TId>.IOffsetStreamDto> offsetStreamHanler)
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

                case ITriggerComponent<TId>.TriggerKind.SimpleStream:
                    {
                        simpleStreamHandler(trigger.SimpleStreamState ?? throw new ArgumentNullException("[Bug]."));
                        break;
                    }

                case ITriggerComponent<TId>.TriggerKind.OffsetStream:
                    {
                        offsetStreamHanler(trigger.OffsetStreamState ?? throw new ArgumentNullException("[Bug]."));
                        break;
                    }

                default: throw new NotImplementedException("[Bug]");
            }
        }

        public async ValueTask OneOfAsync(
            ITriggerComponent<TId> trigger,
            Func<int, ValueTask> counterHandler,
            Func<ValueTask> timerHandler,
            Func<ITriggerComponent<TId>.ISimpleStreamDto, ValueTask> simpleStreamHandler,
            Func<ITriggerComponent<TId>.IOffsetStreamDto, ValueTask> offsetStreamHanler)
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

                case ITriggerComponent<TId>.TriggerKind.SimpleStream:
                    {
                        await simpleStreamHandler(trigger.SimpleStreamState ?? throw new ArgumentNullException("[Bug]."));
                        break;
                    }

                case ITriggerComponent<TId>.TriggerKind.OffsetStream:
                    {
                        await offsetStreamHanler(trigger.OffsetStreamState ?? throw new ArgumentNullException("[Bug]."));
                        break;
                    }

                default: throw new NotImplementedException("[Bug]");
            }
        }
        
    }
}
