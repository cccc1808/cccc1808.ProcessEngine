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

        /// <summary>
        /// <see cref="cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Conditions.ITriggerComponentCondition{TId}"/>
        /// </summary>
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
                                    // Фильтры
                                    e.IsActivated // 1) Активирован
                                    && e.ChildTrigger_WaitDeliveryTimestamp == null // 2) Это не дочерний триггер, который ожидает подтверждения.
                                    && !e.IsCompleted // 3) Не завершен
                                    
                                    // Условия
                                    && e.TimerDate < p.TimerNowDate // 1) Таймер                                    
                                    && e.HandlerKey == p.HandlerKey // 2) Только указанный тип
                                    && Comparer<TId>.Default.Compare(e.Id, p.IdKeysetOffset) > 0 // 3) Keyset пагинация
                                    )
                            .OrderByDescending(e => e.Priority)
                            .ThenBy(e => e.TimerDate)
                            .ThenBy(e => e.HandlerKey) // TODO: тут только для индекса, возможно убрать.
                            .ThenBy(e => e.Id);

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
                                // Фильтры
                                e.IsActivated
                                && e.ChildTrigger_WaitDeliveryTimestamp == null // Дочерний триггер не ждет ответа корневого.
                                && !e.IsCompleted

                                // Условия
                                && e.TimerDate < p.NowDate
                                && p.ids.Contains(e.Id));

                        return s;
                    })
                );
        }
    }
}
