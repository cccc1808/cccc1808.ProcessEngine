using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services
{
    public class TriggerSetter<TId> : ITriggerSetter<TId>
    {
        public ITriggerSetter<TId>.ISimpleStreamSetter SimpleStreamSetter { get; }

        public TriggerSetter(SimpleStreamSetterImplementation.OptionsDto options) 
        {
            SimpleStreamSetter = new SimpleStreamSetterImplementation(options);
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
            TriggerEventKindEnum triggerEventKind, 
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
                TriggerEventKindEnum.CounterEvent => counterTriggerEventHandler(parameters),
                TriggerEventKindEnum.TimerEvent => timerTriggerEventHandler(parameters),
                TriggerEventKindEnum.SimpleStreamEvent => signalSimpleStreamTriggerEventHandler(parameters),
                TriggerEventKindEnum.ProcessGoWaitStreamEvent => processGoWaitStreamTriggerEventHandler(parameters),
                TriggerEventKindEnum.ProcessedOffsetEvent => processedOffsetTriggerEventHandler(parameters),
                TriggerEventKindEnum.SignalOffsetEvent => signalOffsetTriggerEventHandler(parameters),

                _ => throw new NotImplementedException(triggerEventKind.ToString())
            };
        }

        public TResult OneOfEvent<TParameters, TResult>(
            ITriggerEvent triggerEvent,
            TParameters parameters,
            Func<ICounterTriggerEvent, TParameters, TResult> counterTriggerEventHandler,
            Func<ITimerTriggerEvent, TParameters, TResult> timerTriggerEventHandler,
            Func<ISignalSimpleStreamTriggerEvent, TParameters, TResult> signalSimpleStreamTriggerEventHandler,
            Func<IProcessGoWaitStreamTriggerEvent, TParameters, TResult> processGoWaitStreamTriggerEventHandler,
            Func<IProcessedOffsetTriggerEvent, TParameters, TResult> processedOffsetTriggerEventHandler, 
            Func<ISignalOffsetTriggerEvent, TParameters, TResult> signalOffsetTriggerEventHandler)
        {
            return triggerEvent.Kind switch 
            {
                TriggerEventKindEnum.CounterEvent => counterTriggerEventHandler((ICounterTriggerEvent)triggerEvent, parameters),
                TriggerEventKindEnum.TimerEvent => timerTriggerEventHandler((ITimerTriggerEvent)triggerEvent, parameters),
                TriggerEventKindEnum.SimpleStreamEvent => signalSimpleStreamTriggerEventHandler((ISignalSimpleStreamTriggerEvent)triggerEvent, parameters),
                TriggerEventKindEnum.ProcessGoWaitStreamEvent => processGoWaitStreamTriggerEventHandler((IProcessGoWaitStreamTriggerEvent)triggerEvent, parameters),
                TriggerEventKindEnum.ProcessedOffsetEvent => processedOffsetTriggerEventHandler((IProcessedOffsetTriggerEvent)triggerEvent, parameters),
                TriggerEventKindEnum.SignalOffsetEvent => signalOffsetTriggerEventHandler((ISignalOffsetTriggerEvent)triggerEvent, parameters),

                _ => throw new NotImplementedException(triggerEvent.Kind.ToString())
            };
        }

        public class SimpleStreamSetterImplementation 
            : ITriggerSetter<TId>.ISimpleStreamSetter
        {
            private readonly OptionsDto _options;

            public SimpleStreamSetterImplementation(
                OptionsDto options)
            {
                _options = options;
            }

            public void SignalEventReceived(ITriggerComponent.ISimpleStreamDto state)
            {
                if (!_options.NoCounterOptimization)
                {
                    state.NewSignalCounter++;
                }
                else 
                {
                    state.NewSignalCounter = 1;
                }
            }

            public void ProcessGoWaitEventReceived(ITriggerComponent.ISimpleStreamDto state)
            {
                state.StreamsProcessIsWaiting = true;
            }
            
            public bool NeedActivate(ITriggerComponent.ISimpleStreamDto state)
            {
                return state.StreamsProcessIsWaiting && state.NewSignalCounter != 0;
            }

            public void Activated(ITriggerComponent.ISimpleStreamDto state)
            {
                state.StreamsProcessIsWaiting = false;
                state.NewSignalCounter = 0;
            }

            public class OptionsDto() 
            {
                /// <summary>
                /// Оптимизация: счетчик не будет считать количетсов сигналов, а только флаг наличия нового сигнала.
                /// Это позволит делать меньше обновлений БД (если процесс выполняется, то триггер обновиться только на первом сигнале).
                /// TODO: добавить в ITriggerComponent флаг NeedUpdate (чтобы оптимизация работала не только для EF ChangeTracker).
                /// </summary>
                public bool NoCounterOptimization { get; set; }
            }
        }
    }
}
