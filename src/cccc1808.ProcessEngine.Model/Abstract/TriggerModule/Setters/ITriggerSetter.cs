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
        public ISimpleStreamSetter SimpleStreamSetter { get; }

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
            Func<TParameters, TResult> counterTriggerEventHandler,
            Func<TParameters, TResult> timerTriggerEventHandler,
            Func<TParameters, TResult> signalSimpleStreamTriggerEventHandler,
            Func<TParameters, TResult> processGoWaitStreamTriggerEventHandler,
            Func<TParameters, TResult> processedOffsetTriggerEventHandler,
            Func<TParameters, TResult> signalOffsetTriggerEventHandler);

        TResult OneOfEvent<TParameters, TResult>(
            ITriggerEvent triggerEvent, 
            TParameters parameters,
            Func<ICounterTriggerEvent, TParameters, TResult> counterTriggerEventHandler,
            Func<ITimerTriggerEvent, TParameters, TResult> timerTriggerEventHandler,
            Func<ISignalSimpleStreamTriggerEvent, TParameters, TResult> signalSimpleStreamTriggerEventHandler,
            Func<IProcessGoWaitStreamTriggerEvent, TParameters, TResult> processGoWaitStreamTriggerEventHandler,
            Func<IProcessedOffsetTriggerEvent, TParameters, TResult> processedOffsetTriggerEventHandler,
            Func<ISignalOffsetTriggerEvent, TParameters, TResult> signalOffsetTriggerEventHandler);

        void SetActivated(ITriggerComponent<TId> trigger, bool value);

        void SetCompleted(ITriggerComponent<TId> trigger, bool value);

        void SetTimer(ITriggerComponent<TId> trigger, DateTimeOffset value);        

        public interface ISimpleStreamSetter
        {
            /// <summary>
            /// Поступило событие сигнал.
            /// </summary>
            /// <param name="state"></param>
            void SignalEventReceived(ITriggerComponent.ISimpleStreamDto state);

            /// <summary>
            /// Поступило событие о том, что процесс прешел в состояние ожидания>
            /// </summary>
            /// <param name="state"></param>
            void ProcessGoWaitEventReceived(ITriggerComponent.ISimpleStreamDto state);

            /// <summary>
            /// Проверка улосвия небходимости активации триггера.
            /// </summary>
            /// <param name="state"></param>
            /// <returns></returns>
            bool NeedActivate(ITriggerComponent.ISimpleStreamDto state);

            /// <summary>
            /// Триггер был активирован.
            /// </summary>
            /// <param name="state"></param>
            void Activated(ITriggerComponent.ISimpleStreamDto state);
        }
    }
}
