using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components
{
    public interface ITriggerComponent<TId>
    {
        #region prop

        /// <summary>
        /// Ключ триггера.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Тип.
        /// </summary>
        ITriggerComponent.TriggerKind Kind { get; }        

        /// <summary>
        /// Ключ процесса.
        /// </summary>
        TId ProcessId { get; }

        /// <summary>
        /// Активирован.
        /// * Активация тригерия при поступлении <see cref="ITriggerEvent"/>.
        /// * Активация тригера после первого срабатывания (Repeat trigger).
        /// * Активация триггера вручную в БД.
        /// </summary>
        bool IsActivated { get; set; }

        /// <summary>
        /// Триггер завершен.
        /// </summary>
        bool IsCompleted { get; set; }

        /// <summary>
        /// Таймер / задержка выполнения хендлера.
        /// * Реализация таймеров.
        /// * Реализация систем с накоплением (задержкой), чтобы не спамить.
        /// </summary>
        DateTimeOffset TimerDate { get; set; }

        /// <summary>
        /// Ключ хендлера действия.
        /// </summary>
        string HandlerKey { get; }

        DateTimeOffset SelectLockTimeout { get; set; }

        /// <summary>
        /// Кастомное состояние триггера.
        /// </summary>
        object State { get; }

        /// <summary>
        /// Смещение keyset пагинации.
        /// (Пока что используется только страхующим триггером).
        /// </summary>
        TId? OffsetId { get; set; }

        /// <summary>
        /// <see cref="ITriggerComponent.IChildTriggerDto"/>.
        /// Не null если триггер является дочерним.
        /// </summary>
        ITriggerComponent.IChildTriggerDto? ChildTrigger { get; }

        #region InMemory

        bool NeedUpdate { get; set; }

        bool NeedRemove { get; set; }

        #endregion

        #endregion
    }

    public interface ITriggerComponent
    {
        #region types

        public enum TriggerKind
        {
            /// <summary>
            /// Триггер со счетчиком (когда известно количество ожидаемых сигналов).
            /// * Каждый сигнал уменьшает счетчик на 1. Триггер активируется при значении счетчика 0.
            /// * Если в триггер при 0 поступает событие, то он оповторно атктивирует триггер.
            /// У триггера можно использовать поле <see cref="TimerDate"/>.
            /// </summary>
            Counter,

            /// <summary>
            /// Триггер таймер.
            /// * Поступление события активирует триггер.
            /// * Может использоваться без событий (сразу взведенным с задержкой).
            /// </summary>
            Timer,

            /// <summary>
            /// Простой стрим триггер.
            /// * Отслеживает статус засыпания процесса. Не активирует триггер пока процесс не удейт в ожидание.
            /// Как только процесс засыпает сразу реактивирует его (без лишних задержек в отличии от решения на основе таймера).
            /// * Гарантируе оповещение о новом сигнале.
            /// * Используется только в сочетании с wakeup модулем у процесса (чтобы гарантировать, что все сигналы обработаны).
            /// * Особенность: не нужен offset (не треубуется наличе поля упорядоченности у сигналов).
            /// * Особенность: допустимы ложные срабатывания (т.к. тут не известно что процесс обработал, а что нет).
            /// Процесс может уже обработать все сообщения, но все равно будет запущен (без нового сообщения). Должен снова уснуть.
            /// Либо нужна специальаня реализация хендлера триггера, которая будет повторно проверять наличие необработанных сообщений для процесса перед пробуждением.
            /// </summary>
            SimpleStream,
            SimpleStreamRoot,

            /// <summary>
            /// Стрим с отслеживанием смещения (по аналогии с kafka offset).
            /// * Отслеживает статус засыпания процесса. Не активирует триггер пока процесс не удейт в ожидание.
            /// Как только процесс засыпает сразу реактивирует его (без лишних задержек в отличии от решения на основе таймера).
            /// * Оповещает если не все каналы обработаны.
            /// * Может использоваться без wakeup модуля (процесс сработает 1 такт, отправит обработанное смещение и если есть новые то триггер оповестит).
            /// * Особенность: необходима гарантия что сообщения записываются в пордяке нарастания (по offset), не допустима запись сообщения с offset меньше существующей позиции
            /// Если поступит сигнал с меньшим смещение, то сигнал будет утерян т.к. будет считаться, что процесс его уже обработал.
            /// </summary>
            OffsetStream,
        }

        public interface IChildTriggerDto
        {
            /// <summary>
            /// Триггер нужно завершить после подтверждения доставки от корневого триггера.
            /// </summary>
            bool CompleteAfterDelivery { get; set; }

            /// <summary>
            /// Триггер нужно удалить после подтверждения доставки от корневого триггера.
            /// </summary>
            bool RemoveAftrerDelivery { get; set; }

            /// <summary>
            /// Заполнено - сигнал на корневой триггер отправлен, подтверждение не получено.
            /// </summary>
            long? WaitDeliveryTimestamp { get; set; }
        }

        public interface ICounterDto
        {
            /// <summary>
            /// Счетчик.
            /// </summary>
            long Counter { get; set; }
        }

        public interface ISimpleStreamDto
        {
            /// <summary>
            /// Стрим находится в состоянии <see cref="ProcessStatusEnum.AsyncExecute"/> или <see cref="ProcessStatusEnum.WaitEvent"/>.
            /// </summary>
            bool StreamsProcessIsWaiting { get; set; }

            /// <summary>
            /// Счетчик новых сигналов (используется как флаг, счетчик для статистики).
            /// Сбрасывается когда процесс запускается в обработку.
            /// Взводится при поступлении сигнала.
            /// </summary>
            long NewSignalCounter { get; set; }

            /// <summary>
            /// Является ли триггер корневым.
            /// </summary>
            bool IsRootTrigger { get; }
        }

        public interface IOffsetStreamDto
        {
            /// <summary>
            /// Стрим находится в состоянии <see cref="ProcessStatusEnum.AsyncExecute"/> или <see cref="ProcessStatusEnum.WaitEvent"/>.
            /// </summary>
            bool StreamsProcessIsWaiting { get; set; }

            /// <summary>
            /// Наибольшее смещение сигнала.
            /// </summary>
            public long LastOffset { get; set; }

            /// <summary>
            /// Наибольшее обработанное процессом смещение.
            /// </summary>
            public long ProcessedOffset { get; set; }
        }        

        #endregion
    }
}
