using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage
{
    public interface ITriggerQueueContext<TId>
    {
        void IncreseBufferCapacity(int value);

        void SetReserveTimeout(TimeSpan reserveTimeout);

        /// <summary>
        /// * Триггер был создан.
        /// * Триггер перешел в состояние ассинхронного выполнения.
        /// </summary>
        /// <param name="id">id.</param>
        /// <param name="efEntity">Entity, если используется ChangeTracker.</param>
        void TriggerToExecute(TriggerDto trigger);

        /// <summary>
        /// Обработка триггера сработала, но триггер продолжает выполняться.
        /// Продление резерва и помещение в одчередь.
        /// </summary>
        /// <param name="id"></param>
        void TriggerContinueExecute(TriggerDto trigger);        

        /// <summary>
        /// Триггер был обработан и сейчас не выполняется.
        /// Снимает резерв.
        /// </summary>
        /// <param name="id"></param>
        void TriggerExecuted(TId id);        

        /// <summary>
        /// DbSelector выбрал триггер для запуска на выполнение.
        /// Проверяет резервирование и запускает триггер.
        /// </summary>
        /// <returns>Есть ли в очереди еще места.</returns>
        Task<bool> TriggerFromSelector(
            ICollection<TriggerDto> ids,
            DateTimeOffset reserveDate,
            CancellationToken cancellationToken);

        public readonly record struct TriggerDto(
            TId? Id,
            IId<TId>? EfEntity,
            bool IsRangeTrigger,
            TriggerTypeUniqueDto TypeUnique)
        {
            public TId GetId() => EfEntity is null 
                ? Id ?? throw new Exception()
                : EfEntity.Id;

            public static TriggerDto TriggerToExecute(
                TId Id,
                bool IsRangeTrigger,
                TriggerTypeUniqueDto TypeUnique) => new TriggerDto(Id, null, IsRangeTrigger, TypeUnique);

            public static TriggerDto TriggerToExecute(
                IId<TId> EfEntity,
                bool IsRangeTrigger,
                TriggerTypeUniqueDto TypeUnique) => new TriggerDto(default, EfEntity, IsRangeTrigger, TypeUnique);

            public static TriggerDto TriggerContinueRun(
                TId Id,
                bool IsRangeTrigger,
                TriggerTypeUniqueDto TypeUnique) => new TriggerDto(Id, null, IsRangeTrigger, TypeUnique);

            //public static TriggerDto TriggerExecuted(
            //    TId Id,
            //    bool IsRangeTrigger,
            //    string HandlerName) => new TriggerDto(Id, null, IsRangeTrigger, HandlerName);

            public static TriggerDto TriggerFromSelector(
                TId Id,
                bool IsRangeTrigger,
                TriggerTypeUniqueDto TypeUnique) => new TriggerDto(Id, null, IsRangeTrigger, TypeUnique);
        }
    }
}
