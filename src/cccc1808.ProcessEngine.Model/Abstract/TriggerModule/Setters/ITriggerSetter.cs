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
            ITriggerComponent<TId>.TriggerKind kind,
            Action counterHandler,
            Action timerHandler,
            Action simpleStreamHandler,
            Action offsetStreamHanler);

        void OneOf(
            ITriggerComponent<TId> trigger,
            Action<int> counterHandler,
            Action timerHandler,
            Action<ITriggerComponent<TId>.ISimpleStreamDto> simpleStreamHandler,
            Action<ITriggerComponent<TId>.IOffsetStreamDto> offsetStreamHanler
            );

        ValueTask OneOfAsync(
            ITriggerComponent<TId> trigger,
            Func<int, ValueTask> counterHandler,
            Func<ValueTask> timerHandler,
            Func<ITriggerComponent<TId>.ISimpleStreamDto, ValueTask> simpleStreamHandler,
            Func<ITriggerComponent<TId>.IOffsetStreamDto, ValueTask> offsetStreamHanler);        

        void ProcessCounter(ITriggerComponent<TId> trigger, int eventCount);

        bool IsCounterActivated(ITriggerComponent<TId> trigger);

        void SetActivated(ITriggerComponent<TId> trigger, bool value);

        void SetCompleted(ITriggerComponent<TId> trigger, bool value);

        void SetTimer(ITriggerComponent<TId> trigger, DateTimeOffset value);
    }
}
