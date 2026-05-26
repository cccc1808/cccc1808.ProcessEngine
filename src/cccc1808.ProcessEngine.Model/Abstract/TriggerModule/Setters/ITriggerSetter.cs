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
        IOneOfSetter OneOfSetter { get; }

        IStandartSetter StandartSetter { get; }

        ICounterSetter CounterSetter { get; }

        ISimpleStreamSetter SimpleStreamSetter { get; }

        IOffsetStreamSetter OffsetStreamSetter { get; }                   

        public interface IOneOfSetter
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
                TriggerEventKindEnum triggerEventKind,
                TParameters parameters,
                Func<TParameters, TResult> removeTriggerEventHandler,
                Func<TParameters, TResult> counterTriggerEventHandler,
                Func<TParameters, TResult> timerTriggerEventHandler,
                Func<TParameters, TResult> signalSimpleStreamTriggerEventHandler,
                Func<TParameters, TResult> processGoWaitStreamTriggerEventHandler,
                Func<TParameters, TResult> processedOffsetTriggerEventHandler,
                Func<TParameters, TResult> signalOffsetTriggerEventHandler);

            void OneOfEvent<TParameters>(
                ITriggerEvent triggerEvent,
                TParameters parameters,
                Action<IRemoveTriggerEvent, TParameters> removeTriggerEventHandler,
                Action<ICounterTriggerEvent, TParameters> counterTriggerEventHandler,
                Action<ITimerTriggerEvent, TParameters> timerTriggerEventHandler,
                Action<ISignalSimpleStreamTriggerEvent, TParameters> signalSimpleStreamTriggerEventHandler,
                Action<IProcessGoWaitStreamTriggerEvent, TParameters> processGoWaitStreamTriggerEventHandler,
                Action<IProcessedOffsetTriggerEvent, TParameters> processedOffsetTriggerEventHandler,
                Action<ISignalOffsetTriggerEvent, TParameters> signalOffsetTriggerEventHandler);

            TResult OneOfEvent<TParameters, TResult>(
                ITriggerEvent triggerEvent,
                TParameters parameters,
                Func<IRemoveTriggerEvent, TParameters, TResult> removeTriggerEventHandler,
                Func<ICounterTriggerEvent, TParameters, TResult> counterTriggerEventHandler,
                Func<ITimerTriggerEvent, TParameters, TResult> timerTriggerEventHandler,
                Func<ISignalSimpleStreamTriggerEvent, TParameters, TResult> signalSimpleStreamTriggerEventHandler,
                Func<IProcessGoWaitStreamTriggerEvent, TParameters, TResult> processGoWaitStreamTriggerEventHandler,
                Func<IProcessedOffsetTriggerEvent, TParameters, TResult> processedOffsetTriggerEventHandler,
                Func<ISignalOffsetTriggerEvent, TParameters, TResult> signalOffsetTriggerEventHandler);
        }

        public interface IStandartSetter
        {
            void SetActivated(ITriggerComponent<TId> trigger, bool value);

            void SetCompleted(ITriggerComponent<TId> trigger, bool value);

            void SetTimer(ITriggerComponent<TId> trigger, DateTimeOffset value);

            void SetTimer(ITriggerComponent<TId> trigger, in TimerDto value);

            void SetSelectLockTimeout(ITriggerComponent<TId> trigger, DateTimeOffset value);

            void ForRemove(ITriggerComponent<TId> trigger, bool value);

            public readonly record struct TimerDto(
                in DateTimeOffset Now,
                in DateTimeOffset Timer,
                TimeSpan? IfDeltaMore);
        }

        public interface ICounterSetter 
        {
            void CounterEvent(
                ITriggerComponent<TId> trigger,
                ITriggerComponent.ICounterDto state, 
                bool reset, 
                long value);

            bool NeedActivate(ITriggerComponent<TId> trigger, ITriggerComponent.ICounterDto state);

            void Activate(ITriggerComponent<TId> trigger, ITriggerComponent.ICounterDto state);
        }

        public interface ISimpleStreamSetter
        {
            /// <summary>
            /// Поступило событие сигнал.
            /// </summary>
            /// <param name="state"></param>
            void SignalEventReceived(ITriggerComponent<TId> trigger, ITriggerComponent.ISimpleStreamDto state);

            /// <summary>
            /// Поступило событие о том, что процесс прешел в состояние ожидания>
            /// </summary>
            /// <param name="state"></param>
            void ProcessGoWaitEventReceived(ITriggerComponent<TId> trigger, ITriggerComponent.ISimpleStreamDto state);

            /// <summary>
            /// Проверка улосвия небходимости активации триггера.
            /// </summary>
            /// <param name="state"></param>
            /// <returns></returns>
            bool NeedActivate(ITriggerComponent<TId> trigger, ITriggerComponent.ISimpleStreamDto state);

            /// <summary>
            /// Выполнить активацию триггера.
            /// </summary>
            /// <param name="trigger"></param>
            /// <param name="state"></param>
            void Activate(ITriggerComponent<TId> trigger, ITriggerComponent.ISimpleStreamDto state);
        }

        public interface IOffsetStreamSetter 
        {
            /// <summary>
            /// Поступило событие о том, что процесс прешел в состояние ожидания>
            /// </summary>
            /// <param name="state"></param>
            void ProcessGoWaitEventReceived(ITriggerComponent<TId> trigger, ITriggerComponent.IOffsetStreamDto state);

            /// <summary>
            /// Поступило событие об обновление обработанного смещения.
            /// </summary>
            /// <param name="trigger"></param>
            /// <param name="state"></param>
            /// <param name="offset"></param>
            void UpdateProcessedOffset(ITriggerComponent<TId> trigger, ITriggerComponent.IOffsetStreamDto state, long offset);

            /// <summary>
            /// Поступило событие об обновлении общего смещения.
            /// </summary>
            /// <param name="trigger"></param>
            /// <param name="state"></param>
            /// <param name="offset"></param>
            void UpdateLastOffset(ITriggerComponent<TId> trigger, ITriggerComponent.IOffsetStreamDto state, long offset);

            bool NeedActivate(ITriggerComponent<TId> trigger, ITriggerComponent.IOffsetStreamDto state);

            void Activate(ITriggerComponent<TId> trigger, ITriggerComponent.IOffsetStreamDto state);
        }
    }
}
