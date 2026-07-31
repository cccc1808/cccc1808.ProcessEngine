using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Conditions
{
    public class TriggerDbEntityConditions<TId> 
        : ITriggerDbEntityConditions<TId>
    {
        public (
            object? _, 
            IQueryableCondition<TriggerDbEntity<TId>, ICollection<string>> QueryRange)
            Key
        { get; }

        public (
            object? _, 
            IQueryableCondition<TriggerDbEntity<TId>, ICollection<string>> QueryRange
            ) KeyAndNotComplete
        { get; }

        public (
            object? _,
            IQueryableCondition<TriggerDbEntity<TId>, ITriggerDbEntityConditions<TId>.DbProcessingForSelectorParameters> Query)
            DbProcessingForSelector
        { get; }

        public (
            object? _, 
            IQueryableCondition<TriggerDbEntity<TId>, ITriggerDbEntityConditions<TId>.DbProcessingForSelectorParameters> Query) 
            DbProcessingForSelector2
        { get; }

        public (
            object? _, 
            IQueryableCondition<TriggerDbEntity<TId>, ITriggerDbEntityConditions<TId>.DbProcessingForSelectorParameters3> Query
            ) 
            DbProcessingForSelector3
        { get; }


        public (
            object? _, 
            IQueryableCondition<TriggerDbEntity<TId>, ITriggerDbEntityConditions<TId>.DbProcessingForHandlerParameters> Query
            ) DbProcessingForHandler
        { get; }

        public TriggerDbEntityConditions()
        {
            Key = (
                null,
                new DelegateIQueryableCondition<TriggerDbEntity<TId>, ICollection<string>>(
                    (s, p) => s.Where(e => p.Contains(e.Key))));

            KeyAndNotComplete = (
                null,
                new DelegateIQueryableCondition<TriggerDbEntity<TId>, ICollection<string>>(
                    (s, p) => s.Where(e => 
                        p.Contains(e.Key)
                        && !e.IsCompleted)));

            DbProcessingForSelector = (
                null,
                new DelegateIQueryableCondition<TriggerDbEntity<TId>, ITriggerDbEntityConditions<TId>.DbProcessingForSelectorParameters>(
                    (s, p) => 
                    {
                        s = s
                            .Where(
                                e =>
                                    e.IsActivated
                                    && e.ChildTrigger_WaitDeliveryTimestamp == null
                                    && !e.IsCompleted
                                    && e.TimerDate < p.NowDate
                                    && e.ReservationTimeout < p.NowDate
                                    && !p.reservedIds.Contains(e.Id))
                            .OrderByDescending(e => e.Priority);

                        return s;
                    })
                );

            // Добавляет группировку по HandlerKey, что должно быть более оптимально для range триггеров.
            DbProcessingForSelector2 = (
                null,
                new DelegateIQueryableCondition<TriggerDbEntity<TId>, ITriggerDbEntityConditions<TId>.DbProcessingForSelectorParameters>(
                    (s, p) =>
                    {
                        s = s
                            .Where(
                                e =>
                                    e.IsActivated
                                    && e.ChildTrigger_WaitDeliveryTimestamp == null
                                    && !e.IsCompleted
                                    && e.TimerDate < p.NowDate
                                    && e.ReservationTimeout < p.NowDate
                                    && !p.reservedIds.Contains(e.Id))
                            .OrderByDescending(e => e.Priority)
                            .ThenBy(e => e.HandlerKey) // группировка для range триггеров.
                            ;

                        return s;
                    })
                );

            DbProcessingForSelector3 = (
                null,
                new DelegateIQueryableCondition<TriggerDbEntity<TId>, ITriggerDbEntityConditions<TId>.DbProcessingForSelectorParameters3>(
                    (s, p) =>
                    {
                        if (!p.UseSelectLockTable)
                        {
                            s = s
                                .Where(
                                    e =>
                                        e.IsActivated
                                        && e.ChildTrigger_WaitDeliveryTimestamp == null
                                        && !e.IsCompleted
                                        && e.IsRangeHandler == p.IsRangeTrigger
                                        && e.TimerDate < p.NowDate
                                        && e.ReservationTimeout < p.NowDate
                                        && !p.reservedIds.Contains(e.Id))
                                .OrderByDescending(e => e.Priority)
                                .ThenBy(e => e.HandlerKey) // группировка для range триггеров.
                                ;
                        }
                        else
                        {
                            s = s
                                .Where(
                                    e =>
                                        e.IsActivated
                                        && e.ChildTrigger_WaitDeliveryTimestamp == null
                                        && !e.IsCompleted
                                        && e.IsRangeHandler == p.IsRangeTrigger
                                        && e.TimerDate < p.NowDate
                                        && e.ReservationTimeout < p.NowDate
                                        // Отсутсвие записей о блокировке с неистекшей датой.
                                        && !p.DbContext.Set<TriggerReserveDbEntity<TId>>()
                                            .Any(e2 => e2.Id.Equals(e.Id) && e2.ReserveDate < p.NowDate)
                                            )
                                .OrderByDescending(e => e.Priority)
                                .ThenBy(e => e.HandlerKey);
                        }
                        
                        return s;
                    })
                );

            DbProcessingForHandler = (
                null,
                new DelegateIQueryableCondition<TriggerDbEntity<TId>, ITriggerDbEntityConditions<TId>.DbProcessingForHandlerParameters>(
                    (s, p) =>
                    {
                        s = s.Where(
                            e =>
                                e.IsActivated
                                && !e.IsCompleted
                                && e.ChildTrigger_WaitDeliveryTimestamp == null // Дочерний триггер не ждет ответа корневого.
                                && e.TimerDate < p.NowDate
                                && p.ids.Contains(e.Id));

                        return s;
                    })
                );
        }
    }
}
