using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services
{
    public class TriggerSetter<TId> : ITriggerSetter<TId>
    {
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

        public void OneOfTriggerKind<TParamter>(
            ITriggerComponent.TriggerKind kind,
            TParamter paramter,
            Action<TParamter> counterHandler,
            Action<TParamter> timerHandler,
            Action<TParamter> simpleStreamHandler,
            Action<TParamter> offsetStreamHanler)
        {
            switch (kind)
            {
                case ITriggerComponent.TriggerKind.Counter:
                    {
                        counterHandler(paramter);
                        break;
                    }

                case ITriggerComponent.TriggerKind.Timer:
                    {
                        timerHandler(paramter);
                        break;
                    }

                case ITriggerComponent.TriggerKind.SimpleStream:
                    {
                        simpleStreamHandler(paramter);
                        break;
                    }

                case ITriggerComponent.TriggerKind.OffsetStream:
                    {
                        offsetStreamHanler(paramter);
                        break;
                    }

                default: throw new NotImplementedException("[Bug]");
            }
        }

        public void OneOfTrigger<TParameter>(
            ITriggerComponent<TId> trigger, 
            TParameter parameter,
            Action<ITriggerComponent.ICounterDto, TParameter> counterHandler,
            Action<TParameter> timerHandler, 
            Action<ITriggerComponent.ISimpleStreamDto, TParameter> simpleStreamHandler,
            Action<ITriggerComponent.IOffsetStreamDto, TParameter> offsetStreamHanler)
        {
            switch (trigger.Kind)
            {
                case ITriggerComponent.TriggerKind.Counter:
                    {
                        counterHandler((ITriggerComponent.ICounterDto)trigger.State, parameter);
                        break;
                    }

                case ITriggerComponent.TriggerKind.Timer:
                    {
                        timerHandler(parameter);
                        break;
                    }

                case ITriggerComponent.TriggerKind.SimpleStream:
                    {
                        simpleStreamHandler((ITriggerComponent.ISimpleStreamDto)trigger.State, parameter);
                        break;
                    }

                case ITriggerComponent.TriggerKind.OffsetStream:
                    {
                        offsetStreamHanler((ITriggerComponent.IOffsetStreamDto)trigger.State, parameter);
                        break;
                    }

                default: throw new NotImplementedException("[Bug]");
            }
        }

        public async ValueTask OneOfTriggerAsync(
            ITriggerComponent<TId> trigger,
            Func<ITriggerComponent.ICounterDto, ValueTask> counterHandler,
            Func<ValueTask> timerHandler,
            Func<ITriggerComponent.ISimpleStreamDto, ValueTask> simpleStreamHandler,
            Func<ITriggerComponent.IOffsetStreamDto, ValueTask> offsetStreamHanler)
        {
            switch (trigger.Kind)
            {
                case ITriggerComponent.TriggerKind.Counter:
                    {
                        await counterHandler((ITriggerComponent.ICounterDto)trigger.State);
                        break;
                    }

                case ITriggerComponent.TriggerKind.Timer:
                    {
                        await timerHandler();
                        break;
                    }

                case ITriggerComponent.TriggerKind.SimpleStream:
                    {
                        await simpleStreamHandler((ITriggerComponent.ISimpleStreamDto)trigger.State);
                        break;
                    }

                case ITriggerComponent.TriggerKind.OffsetStream:
                    {
                        await offsetStreamHanler((ITriggerComponent.IOffsetStreamDto)trigger.State);
                        break;
                    }

                default: throw new NotImplementedException("[Bug]");
            }
        }

        public TResult OneOfEventKind<TParameters, TResult>(
            ITriggerEvent.KindEnum triggerEventKind, 
            TParameters parameters, 
            Func<TParameters, TResult> counterTriggerEventHandler,
            Func<TParameters, TResult> timerTriggerEventHandler,
            Func<TParameters, TResult> signalSimpleStreamTriggerEventHandler,
            Func<TParameters, TResult> processGoWaitStreamTriggerEventHandler,
            Func<TParameters, TResult> processedOffsetTriggerEventHandler,
            Func<TParameters, TResult> signalOffsetTriggerEventHandler)
        {
            return triggerEventKind switch
            {
                ITriggerEvent.KindEnum.CounterEvent => counterTriggerEventHandler(parameters),
                ITriggerEvent.KindEnum.TimerEvent => timerTriggerEventHandler(parameters),
                ITriggerEvent.KindEnum.SimpleStreamEvent => signalSimpleStreamTriggerEventHandler(parameters),
                ITriggerEvent.KindEnum.ProcessGoWaitStreamEvent => processGoWaitStreamTriggerEventHandler(parameters),
                ITriggerEvent.KindEnum.ProcessedOffsetEvent => processedOffsetTriggerEventHandler(parameters),
                ITriggerEvent.KindEnum.SignalOffsetEvent => signalOffsetTriggerEventHandler(parameters),

                _ => throw new NotImplementedException(triggerEventKind.ToString())
            };
        }

        public TResult OneOfEvent<TParameters, TResult>(
            ITriggerEvent<TId> triggerEvent,
            TParameters parameters,
            Func<ICounterTriggerEvent<TId>, TParameters, TResult> counterTriggerEventHandler,
            Func<ITimerTriggerEvent<TId>, TParameters, TResult> timerTriggerEventHandler,
            Func<ISignalSimpleStreamTriggerEvent<TId>, TParameters, TResult> signalSimpleStreamTriggerEventHandler,
            Func<IProcessGoWaitStreamTriggerEvent<TId>, TParameters, TResult> processGoWaitStreamTriggerEventHandler,
            Func<IProcessedOffsetTriggerEvent<TId>, TParameters, TResult> processedOffsetTriggerEventHandler, 
            Func<ISignalOffsetTriggerEvent<TId>, TParameters, TResult> signalOffsetTriggerEventHandler)
        {
            return triggerEvent.Kind switch 
            {
                ITriggerEvent.KindEnum.CounterEvent => counterTriggerEventHandler((ICounterTriggerEvent<TId>)triggerEvent, parameters),
                ITriggerEvent.KindEnum.TimerEvent => timerTriggerEventHandler((ITimerTriggerEvent<TId>)triggerEvent, parameters),
                ITriggerEvent.KindEnum.SimpleStreamEvent => signalSimpleStreamTriggerEventHandler((ISignalSimpleStreamTriggerEvent<TId>)triggerEvent, parameters),
                ITriggerEvent.KindEnum.ProcessGoWaitStreamEvent => processGoWaitStreamTriggerEventHandler((IProcessGoWaitStreamTriggerEvent<TId>)triggerEvent, parameters),
                ITriggerEvent.KindEnum.ProcessedOffsetEvent => processedOffsetTriggerEventHandler((IProcessedOffsetTriggerEvent<TId>)triggerEvent, parameters),
                ITriggerEvent.KindEnum.SignalOffsetEvent => signalOffsetTriggerEventHandler((ISignalOffsetTriggerEvent<TId>)triggerEvent, parameters),

                _ => throw new NotImplementedException(triggerEvent.Kind.ToString())
            };
        }
    }
}
