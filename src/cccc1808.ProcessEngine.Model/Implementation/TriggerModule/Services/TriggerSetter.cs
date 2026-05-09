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
        public ITriggerSetter<TId>.IOneOfSetter OneOfSetter { get; }

        public ITriggerSetter<TId>.IStandartSetter StandartSetter { get; }

        public ITriggerSetter<TId>.ICounterSetter CounterSetter { get; }

        public ITriggerSetter<TId>.ISimpleStreamSetter SimpleStreamSetter { get; }
        
        public ITriggerSetter<TId>.IOffsetStreamSetter OffsetStreamSetter { get; }

        public TriggerSetter(
            ITriggerSetter<TId>.IOneOfSetter oneOfSetter,
            ITriggerSetter<TId>.IStandartSetter standartSetter,
            ITriggerSetter<TId>.ICounterSetter counterSetter,
            ITriggerSetter<TId>.ISimpleStreamSetter simpleStreamSetter,
            ITriggerSetter<TId>.IOffsetStreamSetter offsetStreamSetter
            )
        {
            OneOfSetter = oneOfSetter;
            StandartSetter = standartSetter;
            CounterSetter = counterSetter;
            SimpleStreamSetter = simpleStreamSetter;
            OffsetStreamSetter = offsetStreamSetter;            
        }

        public class OneOfSetterImpl 
            : ITriggerSetter<TId>.IOneOfSetter
        {
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
        }

        public class StandartSetterImpl 
            : ITriggerSetter<TId>.IStandartSetter
        {
            public void SetActivated(ITriggerComponent<TId> trigger, bool value)
            {
                if (trigger.IsActivated != value)
                {
                    trigger.IsActivated = value;
                    trigger.NeedUpdate = true;
                }
            }

            public void SetCompleted(ITriggerComponent<TId> trigger, bool value)
            {
                if (trigger.IsCompleted != value)
                {
                    trigger.IsCompleted = value;
                    trigger.NeedUpdate = true;
                }
            }

            public void SetTimer(ITriggerComponent<TId> trigger, DateTimeOffset value)
            {
                if (trigger.TimerDate != value)
                {
                    trigger.TimerDate = value;
                    trigger.NeedUpdate = true;
                }
            }

            public void ForRemove(ITriggerComponent<TId> trigger, bool value)
            {
                trigger.NeedRemove = value;
            }
        }

        public class CounterSetterImpl 
            : ITriggerSetter<TId>.ICounterSetter
        {
            private readonly ITriggerSetter<TId>.IStandartSetter _standartSetter;

            public CounterSetterImpl(
                ITriggerSetter<TId>.IStandartSetter standartSetter)
            {
                _standartSetter = standartSetter;
            }

            public void CounterEvent(
                ITriggerComponent<TId> trigger, 
                ITriggerComponent.ICounterDto state,
                bool reset,
                long value)
            {
                if (reset)
                {
                    if (state.Counter != value)
                    {
                        state.Counter = value;
                        trigger.NeedUpdate = true;
                    }
                }
                else 
                {
                    state.Counter += value;
                    trigger.NeedUpdate = true;
                }
            }

            public bool NeedActivate(ITriggerComponent<TId> trigger, ITriggerComponent.ICounterDto state)
            {
                return
                    !trigger.IsActivated
                    && state.Counter <= 0;
            }

            public void Activate(ITriggerComponent<TId> trigger, ITriggerComponent.ICounterDto state)
            {
                _standartSetter.SetActivated(trigger, true);
            }
        }

        public class SimpleStreamSetterImpl 
            : ITriggerSetter<TId>.ISimpleStreamSetter
        {
            private readonly ITriggerSetter<TId>.IStandartSetter _standartSetter;
            private readonly OptionsDto _options;

            public SimpleStreamSetterImpl(
                ITriggerSetter<TId>.IStandartSetter standartSetter,
                OptionsDto options)
            {
                _standartSetter = standartSetter;
                _options = options;
            }

            public void SignalEventReceived(ITriggerComponent<TId> trigger, ITriggerComponent.ISimpleStreamDto state)
            {
                if (!_options.NoCounterOptimization)
                {
                    state.NewSignalCounter++;
                    trigger.NeedUpdate = true;
                }
                else 
                {
                    if (state.NewSignalCounter == 0)
                    {
                        state.NewSignalCounter = 1;
                        trigger.NeedUpdate = true;
                    }                    
                }
            }

            public void ProcessGoWaitEventReceived(ITriggerComponent<TId> trigger, ITriggerComponent.ISimpleStreamDto state)
            {
                if (!state.StreamsProcessIsWaiting)
                {
                    state.StreamsProcessIsWaiting = true;
                    trigger.NeedUpdate = true;
                }                
            }
            
            public bool NeedActivate(ITriggerComponent<TId> trigger, ITriggerComponent.ISimpleStreamDto state)
            {
                return
                    !trigger.IsActivated
                    && state.StreamsProcessIsWaiting
                    && state.NewSignalCounter != 0;
            }

            public void Activate(ITriggerComponent<TId> trigger, ITriggerComponent.ISimpleStreamDto state)
            {
                // Процесс на пробуждение, счетчик сбрасывается.
                _standartSetter.SetActivated(trigger, true);

                state.StreamsProcessIsWaiting = false;
                state.NewSignalCounter = 0;

                trigger.NeedUpdate = true;
            }

            public class OptionsDto() 
            {
                /// <summary>
                /// Оптимизация: счетчик не будет считать количетсов сигналов, а только флаг наличия нового сигнала.
                /// Это позволит делать меньше обновлений БД (если процесс выполняется, то триггер обновиться только на первом сигнале).
                /// </summary>
                public bool NoCounterOptimization { get; set; }
            }
        }

        public class OffsetStreamSetterImpl : ITriggerSetter<TId>.IOffsetStreamSetter
        {
            private readonly ITriggerSetter<TId>.IStandartSetter _standartSetter;

            public OffsetStreamSetterImpl(
                ITriggerSetter<TId>.IStandartSetter standartSetter)
            {
                _standartSetter = standartSetter;
            }

            public void ProcessGoWaitEventReceived(ITriggerComponent<TId> trigger, ITriggerComponent.IOffsetStreamDto state)
            {
                if (!state.StreamsProcessIsWaiting)
                {
                    state.StreamsProcessIsWaiting = true;
                    trigger.NeedUpdate = true;
                }
            }

            public void UpdateProcessedOffset(ITriggerComponent<TId> trigger, ITriggerComponent.IOffsetStreamDto state, long offset)
            {
                if (state.ProcessedOffset != offset)
                {
                    state.ProcessedOffset = offset;
                    trigger.NeedUpdate = true;
                }
            }

            public void UpdateLastOffset(ITriggerComponent<TId> trigger, ITriggerComponent.IOffsetStreamDto state, long offset)
            {
                if (state.LastOffset != offset)
                {
                    state.LastOffset = offset;
                    trigger.NeedUpdate = true;
                }
            }            

            public bool NeedActivate(ITriggerComponent<TId> trigger, ITriggerComponent.IOffsetStreamDto state)
            {
                return
                    !trigger.IsActivated
                    && state.StreamsProcessIsWaiting
                    && state.ProcessedOffset < state.LastOffset;
            }

            public void Activate(ITriggerComponent<TId> trigger, ITriggerComponent.IOffsetStreamDto state)
            {
                _standartSetter.SetActivated(trigger, true);
                state.StreamsProcessIsWaiting = false;

                trigger.NeedUpdate = true;
            }
        }
    }
}
