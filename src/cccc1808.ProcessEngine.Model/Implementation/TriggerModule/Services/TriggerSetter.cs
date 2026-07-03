using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services
{
    public class TriggerSetter<TId> : ITriggerSetter<TId>
    {
        public ITriggerSetter<TId>.IOneOfTriggerSetter OneOfTriggerSetter { get; }

        public ITriggerSetter<TId>.IOneOfTriggerEventSetter OneOfTriggerEventSetter { get; }

        public ITriggerSetter<TId>.IStandartSetter StandartSetter { get; }        

        public ITriggerSetter<TId>.IChildTriggerSetter ChildTriggerSetter { get; }

        public ITriggerSetter<TId>.ICounterSetter CounterSetter { get; }

        public ITriggerSetter<TId>.IStreamSetter StreamSetter { get; }

        public ITriggerSetter<TId>.ISimpleStreamSetter SimpleStreamSetter { get; }
        
        public ITriggerSetter<TId>.IOffsetStreamSetter OffsetStreamSetter { get; }       


        public TriggerSetter(
            ITriggerSetter<TId>.IOneOfTriggerSetter oneOfTriggerSetter,
            ITriggerSetter<TId>.IOneOfTriggerEventSetter oneOfTriggerEventSetter,
            ITriggerSetter<TId>.IStandartSetter standartSetter,
            ITriggerSetter<TId>.IChildTriggerSetter childTriggerSetter,
            ITriggerSetter<TId>.ICounterSetter counterSetter,
            ITriggerSetter<TId>.IStreamSetter streamSetter,
            ITriggerSetter<TId>.ISimpleStreamSetter simpleStreamSetter,
            ITriggerSetter<TId>.IOffsetStreamSetter offsetStreamSetter
            )
        {
            OneOfTriggerSetter = oneOfTriggerSetter;
            OneOfTriggerEventSetter = oneOfTriggerEventSetter;
            StandartSetter = standartSetter;
            ChildTriggerSetter = childTriggerSetter;
            CounterSetter = counterSetter;
            StreamSetter = streamSetter;
            SimpleStreamSetter = simpleStreamSetter;
            OffsetStreamSetter = offsetStreamSetter;            
        }

        public class OneOfTriggerSetterImpl
            : ITriggerSetter<TId>.IOneOfTriggerSetter
        {
            public void OneOfKind<TParamter>(
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
                    case ITriggerComponent.TriggerKind.SimpleStreamRoot:
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

            public void OneOf<TParameter>(
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
                    case ITriggerComponent.TriggerKind.SimpleStreamRoot:
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

            public async ValueTask OneOfAsync(
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
                    case ITriggerComponent.TriggerKind.SimpleStreamRoot:
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
        }

        public class OneOfTriggerEventSetterImpl
            : ITriggerSetter<TId>.IOneOfTriggerEventSetter
        {
            public TResult OneOfKind<TParameters, TResult>(
                TriggerEventKindEnum triggerEventKind,
                TParameters parameters,
                Func<TParameters, TResult> removeTriggerEventHandler,
                Func<TParameters, TResult> counterTriggerEventHandler,
                Func<TParameters, TResult> timerTriggerEventHandler,
                Func<TParameters, TResult> signalSimpleStreamTriggerEventHandler,
                Func<TParameters, TResult> processGoWaitStreamTriggerEventHandler,
                Func<TParameters, TResult> ignoreCodeSimpleStreamTriggerEventHandler,
                Func<TParameters, TResult> recheckIgnoreRootTriggerEventHandler,
                Func<TParameters, TResult> processedOffsetTriggerEventHandler,
                Func<TParameters, TResult> signalOffsetTriggerEventHandler,
                Func<TParameters, TResult> recheckProcessStatusStreamTriggerEventHandler,
                Func<TParameters, TResult> deliveryResultEventHandler)
            {
                return triggerEventKind switch
                {
                    TriggerEventKindEnum.RemoveTriggerEvent => removeTriggerEventHandler(parameters),
                    TriggerEventKindEnum.CounterEvent => counterTriggerEventHandler(parameters),
                    TriggerEventKindEnum.TimerEvent => timerTriggerEventHandler(parameters),
                    TriggerEventKindEnum.SimpleStreamEvent => signalSimpleStreamTriggerEventHandler(parameters),
                    TriggerEventKindEnum.ProcessGoWaitStreamEvent => processGoWaitStreamTriggerEventHandler(parameters),
                    TriggerEventKindEnum.FilterSignalRootTriggerEvent => ignoreCodeSimpleStreamTriggerEventHandler(parameters),
                    TriggerEventKindEnum.RecheckIgnoreRootTriggerEvent => recheckIgnoreRootTriggerEventHandler(parameters),
                    TriggerEventKindEnum.ProcessedOffsetEvent => processedOffsetTriggerEventHandler(parameters),
                    TriggerEventKindEnum.SignalOffsetEvent => signalOffsetTriggerEventHandler(parameters),
                    TriggerEventKindEnum.RecheckProcessStatusStreamTriggerEvent => recheckProcessStatusStreamTriggerEventHandler(parameters),
                    TriggerEventKindEnum.DeliveryResultEvent => deliveryResultEventHandler(parameters),

                    _ => throw new NotImplementedException(triggerEventKind.ToString())
                };
            }

            public TResult OneOf<TParameters, TResult>(
                ITriggerEvent triggerEvent,
                TParameters parameters,
                Func<IRemoveTriggerEvent, TParameters, TResult> removeTriggerEventHandler,
                Func<ICounterTriggerEvent, TParameters, TResult> counterTriggerEventHandler,
                Func<ITimerTriggerEvent, TParameters, TResult> timerTriggerEventHandler,
                Func<ISignalSimpleStreamTriggerEvent, TParameters, TResult> signalSimpleStreamTriggerEventHandler,
                Func<IProcessGoWaitStreamTriggerEvent, TParameters, TResult> processGoWaitStreamTriggerEventHandler,
                Func<IFilterSignalRootTriggerEvent, TParameters, TResult> ignoreCodeSimpleStreamTriggerEventHandler,
                Func<IRecheckSignalFilterRootTriggerEvent, TParameters, TResult> recheckIgnoreRootTriggerEventHandler,
                Func<IProcessedOffsetTriggerEvent, TParameters, TResult> processedOffsetTriggerEventHandler,
                Func<ISignalOffsetTriggerEvent, TParameters, TResult> signalOffsetTriggerEventHandler,
                Func<IRecheckProcessStatusStreamTriggerEvent, TParameters, TResult> recheckProcessStatusStreamTriggerEventHandler,
                Func<IDeliveryResultEvent, TParameters, TResult> deliveryResultEventHandler)
            {
                return triggerEvent.Kind switch
                {
                    TriggerEventKindEnum.RemoveTriggerEvent => removeTriggerEventHandler((IRemoveTriggerEvent)triggerEvent, parameters),
                    TriggerEventKindEnum.CounterEvent => counterTriggerEventHandler((ICounterTriggerEvent)triggerEvent, parameters),
                    TriggerEventKindEnum.TimerEvent => timerTriggerEventHandler((ITimerTriggerEvent)triggerEvent, parameters),
                    TriggerEventKindEnum.SimpleStreamEvent => signalSimpleStreamTriggerEventHandler((ISignalSimpleStreamTriggerEvent)triggerEvent, parameters),
                    TriggerEventKindEnum.ProcessGoWaitStreamEvent => processGoWaitStreamTriggerEventHandler((IProcessGoWaitStreamTriggerEvent)triggerEvent, parameters),
                    TriggerEventKindEnum.ProcessedOffsetEvent => processedOffsetTriggerEventHandler((IProcessedOffsetTriggerEvent)triggerEvent, parameters),
                    TriggerEventKindEnum.SignalOffsetEvent => signalOffsetTriggerEventHandler((ISignalOffsetTriggerEvent)triggerEvent, parameters),
                    TriggerEventKindEnum.RecheckProcessStatusStreamTriggerEvent => recheckProcessStatusStreamTriggerEventHandler((IRecheckProcessStatusStreamTriggerEvent)triggerEvent, parameters),
                    TriggerEventKindEnum.DeliveryResultEvent => deliveryResultEventHandler((IDeliveryResultEvent)triggerEvent, parameters),

                    _ => throw new NotImplementedException(triggerEvent.Kind.ToString())
                };
            }

            public void OneOf<TParameters>(
                ITriggerEvent triggerEvent,
                TParameters parameters,
                Action<IRemoveTriggerEvent, TParameters> removeTriggerEventHandler,
                Action<ICounterTriggerEvent, TParameters> counterTriggerEventHandler,
                Action<ITimerTriggerEvent, TParameters> timerTriggerEventHandler,
                Action<ISignalSimpleStreamTriggerEvent, TParameters> signalSimpleStreamTriggerEventHandler,
                Action<IProcessGoWaitStreamTriggerEvent, TParameters> processGoWaitStreamTriggerEventHandler,
                Action<IFilterSignalRootTriggerEvent, TParameters> ignoreCodeSimpleStreamTriggerEventHandler,
                Action<IRecheckSignalFilterRootTriggerEvent, TParameters> recheckIgnoreRootTriggerEventHandler,
                Action<IProcessedOffsetTriggerEvent, TParameters> processedOffsetTriggerEventHandler,
                Action<ISignalOffsetTriggerEvent, TParameters> signalOffsetTriggerEventHandler,
                Action<IRecheckProcessStatusStreamTriggerEvent, TParameters> recheckProcessStatusStreamTriggerEventHandler,
                Action<IDeliveryResultEvent, TParameters> deliveryResultEventHandler)
            {
                switch (triggerEvent.Kind)
                {
                    case TriggerEventKindEnum.RemoveTriggerEvent:
                        {
                            removeTriggerEventHandler((IRemoveTriggerEvent)triggerEvent, parameters);
                            break;
                        }

                    case TriggerEventKindEnum.CounterEvent:
                        {
                            counterTriggerEventHandler((ICounterTriggerEvent)triggerEvent, parameters);
                            break;
                        }

                    case TriggerEventKindEnum.TimerEvent:
                        {
                            timerTriggerEventHandler((ITimerTriggerEvent)triggerEvent, parameters);
                            break;
                        }
                    case TriggerEventKindEnum.SimpleStreamEvent:
                        {
                            signalSimpleStreamTriggerEventHandler((ISignalSimpleStreamTriggerEvent)triggerEvent, parameters);
                            break;
                        }
                    case TriggerEventKindEnum.ProcessGoWaitStreamEvent:
                        {
                            processGoWaitStreamTriggerEventHandler((IProcessGoWaitStreamTriggerEvent)triggerEvent, parameters);
                            break;
                        }
                    case TriggerEventKindEnum.ProcessedOffsetEvent:
                        {
                            processedOffsetTriggerEventHandler((IProcessedOffsetTriggerEvent)triggerEvent, parameters);
                            break;
                        }
                    case TriggerEventKindEnum.SignalOffsetEvent:
                        {
                            signalOffsetTriggerEventHandler((ISignalOffsetTriggerEvent)triggerEvent, parameters);
                            break;
                        }

                    case TriggerEventKindEnum.RecheckProcessStatusStreamTriggerEvent:
                        {
                            recheckProcessStatusStreamTriggerEventHandler((IRecheckProcessStatusStreamTriggerEvent)triggerEvent, parameters);
                            break;
                        }

                    case TriggerEventKindEnum.DeliveryResultEvent:
                        {
                            deliveryResultEventHandler((IDeliveryResultEvent)triggerEvent, parameters);
                            break;
                        }

                    default:
                        throw new NotImplementedException(triggerEvent.Kind.ToString());
                }
            }
        }

        public class StandartSetterImpl 
            : ITriggerSetter<TId>.IStandartSetter
        {
            private readonly IDateTimeProvider _dateTimeProvider;

            public StandartSetterImpl(IDateTimeProvider dateTimeProvider)
            {
                _dateTimeProvider = dateTimeProvider;
            }

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

            public void SetTimer(
                ITriggerComponent<TId> trigger,
                in ITriggerSetter<TId>.IStandartSetter.TimerDto value)
            {
                if (value.IfDeltaMore.HasValue)
                {
                    // Обновляем таймер, только если оставшаяся дельта больше указанного параметра.
                    if ((trigger.TimerDate - value.Now) > value.IfDeltaMore)
                    {
                        SetTimer(trigger, value.Timer);
                    }
                }
                else
                {
                    SetTimer(trigger, value.Timer);
                }
            }

            public void ForRemove(ITriggerComponent<TId> trigger, bool value)
            {
                trigger.NeedRemove = value;
            }

            public void SetSelectLockTimeout(ITriggerComponent<TId> trigger, DateTimeOffset value)
            {
                if (trigger.SelectLockTimeout != value)
                {
                    trigger.SelectLockTimeout = value;
                    trigger.NeedUpdate = true;
                }
            }

            public void SetTriggerResult(ITriggerComponent<TId> trigger, ITriggerHandler.ResultDto result)
            {
                if (result.NeedRemove)
                {
                    ForRemove(trigger, value: true);
                    SetCompleted(trigger, true);
                    SetActivated(trigger, false);
                                     
                }
                else 
                {
                    if (result.NeedRepeat)
                    {
                        SetTimer(trigger, result.ExecuteTimeout);
                        SetActivated(trigger, result.IsActivated);
                        SetCompleted(trigger, false);
                    }
                    else
                    {
                        SetActivated(trigger, false);
                        SetCompleted(trigger, true);
                    }

                    SetSelectLockTimeout(trigger, _dateTimeProvider.UtcNow);
                }                
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

        public class StreamSetterImpl 
            : ITriggerSetter<TId>.IStreamSetter
        {
            private readonly ITriggerSetter<TId>.IOneOfTriggerSetter _oneOfTriggerSetter;

            public StreamSetterImpl(
                ITriggerSetter<TId>.IOneOfTriggerSetter oneOfTriggerSetter)
            {
                _oneOfTriggerSetter = oneOfTriggerSetter;
            }

            public bool IsStreamTrigger(ITriggerComponent<TId> trigger)
            {
                return trigger.Kind
                    is ITriggerComponent.TriggerKind.SimpleStream
                    or ITriggerComponent.TriggerKind.OffsetStream
                    or ITriggerComponent.TriggerKind.SimpleStreamRoot;
            }

            public bool GetStreamsProcessIsWaiting(ITriggerComponent<TId> trigger)
            {
                var result = LinkContainer.Create(false);
                _oneOfTriggerSetter.OneOf(
                    trigger,
                    result,
                    counterHandler: static (_, _) => throw new ArgumentException("[Bug]. Ожидается stream триггер."),
                    timerHandler: static (_) => throw new ArgumentException("[Bug]. Ожидается stream триггер."),
                    simpleStreamHandler: static (state, p) => p.Data = state.StreamsProcessIsWaiting,
                    offsetStreamHanler: static (state, p) => p.Data = state.StreamsProcessIsWaiting);

                return result.Data;
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
                // Если процесс уснул и не все смещение обработано.
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

        public class ChildTriggerSetterImpl 
            : ITriggerSetter<TId>.IChildTriggerSetter
        {
            private readonly ITriggerSetter<TId>.IStandartSetter _standartSetter;

            public ChildTriggerSetterImpl(ITriggerSetter<TId>.IStandartSetter standartSetter)
            {
                _standartSetter = standartSetter;
            }

            public bool IsChildTrigger(
                ITriggerComponent<TId> trigger, 
                out ITriggerComponent.IChildTriggerDto state)
            {
                state = trigger.ChildTrigger!;
                return state != null;
            }

            public void SetTriggerResult(
                ITriggerComponent<TId> trigger,
                ITriggerComponent.IChildTriggerDto state,
                ITriggerHandler.ResultDto result, 
                long deliveryTimestamp)
            {
                _standartSetter.SetTriggerResult(trigger, result);

                if (trigger.NeedRemove)
                {
                    // Если тригерр помечен на удаление, то сохраняем флаг для удаления только после получения подтвреждения.
                    // Если триггер удалить сейчас, то не будет корректной обработки события подтверждения.
                    state.RemoveAftrerDelivery = true;
                    _standartSetter.ForRemove(trigger, value: false);
                }
                if (trigger.IsCompleted)
                {
                    // Если триггер помечен как завершенный, то сохраняем флаг и завершаем после получения подтверждения.
                    // Если завершить триггер сейчас, то он не будет обрабатывать события.
                    state.CompleteAfterDelivery = true;
                    _standartSetter.SetCompleted(trigger, value: false);
                }

                state.WaitDeliveryTimestamp = deliveryTimestamp;
                trigger.NeedUpdate = true;
            }

            public void RepeatSignalSended(
                ITriggerComponent<TId> trigger,
                ITriggerComponent.IChildTriggerDto state,
                long deliveryTimestamp)
            {
                state.WaitDeliveryTimestamp = deliveryTimestamp;
                trigger.NeedUpdate = true;
            }

            public void DeliveryResultReceived(
                ITriggerComponent<TId> trigger,
                ITriggerComponent.IChildTriggerDto state,
                long deliveryTimestamp)
            {
                if (state.WaitDeliveryTimestamp != deliveryTimestamp)
                {
                    // TODO: log warning;
                    return;
                }

                if (state.WaitDeliveryTimestamp != null)
                {
                    // Подтверждение доставки получено.
                    state.WaitDeliveryTimestamp = null;
                    trigger.NeedUpdate = true;
                }

                if (state.RemoveAftrerDelivery)
                {
                    _standartSetter.ForRemove(trigger, value: true);
                    state.RemoveAftrerDelivery = false;
                    trigger.NeedUpdate = true;
                }

                if (state.CompleteAfterDelivery)
                {
                    _standartSetter.SetCompleted(trigger, value: true);
                    state.CompleteAfterDelivery = false;
                    trigger.NeedUpdate = true;
                }
            }

            public void SetSignalCode(ITriggerComponent<TId> trigger, in BitFlagDto value)
            {
                if (trigger.SignalCode.Bits == value.Bits)
                {
                    return;
                }

                trigger.SignalCode = value;
                trigger.NeedUpdate = true;
            }

            public void SetIgnoreCode(ITriggerComponent<TId> trigger, in BitFlagDto value)
            {
                if (trigger.IgnoreSignalCode.Bits == value.Bits)
                {
                    return;
                }

                trigger.IgnoreSignalCode = value;
                trigger.NeedUpdate = true;
            }

            public long DateToTimestamp(DateTimeOffset date)
            {
                return date.Ticks;
            }

            public DateTimeOffset TimestampToDate(long timestamp)
            {
                return new DateTimeOffset(timestamp, DateTimeOffset.UtcNow.Offset);
            }            
        }
    }
}
