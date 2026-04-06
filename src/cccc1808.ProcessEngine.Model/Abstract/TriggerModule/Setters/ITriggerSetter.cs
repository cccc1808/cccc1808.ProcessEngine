using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters
{
    public interface ITriggerSetter<TId>
    {
        void OneOf(
            ITriggerComponent<TId> trigger,
            Action<int> counterHandler,
            Action timerHandler)
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

                default: throw new NotImplementedException("[Bug]");
            }
        }

        async ValueTask OneOfAsync(
            ITriggerComponent<TId> trigger, 
            Func<int, ValueTask> counterHandler,
            Func<ValueTask> timerHandler)
        {
            switch(trigger.Kind)
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

                default: throw new NotImplementedException("[Bug]");
            }
        }

        void ProcessCounter(ITriggerComponent<TId> trigger, int eventCount);

        bool IsCounterActivated(ITriggerComponent<TId> trigger);

        void SetActivated(ITriggerComponent<TId> trigger, bool value);

        void SetCompleted(ITriggerComponent<TId> trigger, bool value);

        void SetTimer(ITriggerComponent<TId> trigger, DateTimeOffset value);
    }
}
