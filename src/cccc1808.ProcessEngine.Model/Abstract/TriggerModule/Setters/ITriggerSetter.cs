using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters
{
    public interface ITriggerSetter<TId>
    {
        void OneOfTriggerKind<TParamter>(
            ITriggerComponent.TriggerKind kind,
            TParamter paramter,
            Action<TParamter> counterHandler,
            Action<TParamter> timerHandler,
            Action<TParamter> simpleStreamHandler,
            Action<TParamter> offsetStreamHanler);

        void OneOfTrigger<TParameter>(
            ITriggerComponent<TId> trigger,
            TParameter parameter,
            Action<ITriggerComponent.ICounterDto, TParameter> counterHandler,
            Action<TParameter> timerHandler,
            Action<ITriggerComponent.ISimpleStreamDto, TParameter> simpleStreamHandler,
            Action<ITriggerComponent.IOffsetStreamDto, TParameter> offsetStreamHanler
            );

        ValueTask OneOfTriggerAsync(
            ITriggerComponent<TId> trigger,
            Func<ITriggerComponent.ICounterDto, ValueTask> counterHandler,
            Func<ValueTask> timerHandler,
            Func<ITriggerComponent.ISimpleStreamDto, ValueTask> simpleStreamHandler,
            Func<ITriggerComponent.IOffsetStreamDto, ValueTask> offsetStreamHanler);

        TResult OneOfEventKind<TParameters, TResult>(
            ITriggerEvent.KindEnum triggerEventKind,
            TParameters parameters,
            Func<TParameters, TResult> counterTriggerEventHandler,
            Func<TParameters, TResult> timerTriggerEventHandler,
            Func<TParameters, TResult> signalSimpleStreamTriggerEventHandler,
            Func<TParameters, TResult> processGoWaitStreamTriggerEventHandler,
            Func<TParameters, TResult> processedOffsetTriggerEventHandler,
            Func<TParameters, TResult> signalOffsetTriggerEventHandler);

        TResult OneOfEvent<TParameters, TResult>(
            ITriggerEvent<TId> triggerEvent, 
            TParameters parameters,
            Func<ICounterTriggerEvent<TId>, TParameters, TResult> counterTriggerEventHandler,
            Func<ITimerTriggerEvent<TId>, TParameters, TResult> timerTriggerEventHandler,
            Func<ISignalSimpleStreamTriggerEvent<TId>, TParameters, TResult> signalSimpleStreamTriggerEventHandler,
            Func<IProcessGoWaitStreamTriggerEvent<TId>, TParameters, TResult> processGoWaitStreamTriggerEventHandler,
            Func<IProcessedOffsetTriggerEvent<TId>, TParameters, TResult> processedOffsetTriggerEventHandler,
            Func<ISignalOffsetTriggerEvent<TId>, TParameters, TResult> signalOffsetTriggerEventHandler);

        void SetActivated(ITriggerComponent<TId> trigger, bool value);

        void SetCompleted(ITriggerComponent<TId> trigger, bool value);

        void SetTimer(ITriggerComponent<TId> trigger, DateTimeOffset value);
    }
}
