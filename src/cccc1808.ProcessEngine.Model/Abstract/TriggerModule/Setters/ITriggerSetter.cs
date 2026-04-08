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
            Action timerHandler,
            Action<bool, IReadOnlyDictionary<string, long>, IReadOnlyDictionary<string, long>> streamHandler
            );

        ValueTask OneOfAsync(
            ITriggerComponent<TId> trigger,
            Func<int, ValueTask> counterHandler,
            Func<ValueTask> timerHandler,
            Func<bool, IReadOnlyDictionary<string, long>, IReadOnlyDictionary<string, long>, ValueTask> streamHandler);        

        void ProcessCounter(ITriggerComponent<TId> trigger, int eventCount);

        bool IsCounterActivated(ITriggerComponent<TId> trigger);

        void SetActivated(ITriggerComponent<TId> trigger, bool value);

        void SetCompleted(ITriggerComponent<TId> trigger, bool value);

        void SetTimer(ITriggerComponent<TId> trigger, DateTimeOffset value);

        void SetStreamsState(
            ITriggerComponent<TId> trigger,
            bool processIsWaiting,
            Dictionary<string, long> channels,
            Dictionary<string, long> process);
    }
}
