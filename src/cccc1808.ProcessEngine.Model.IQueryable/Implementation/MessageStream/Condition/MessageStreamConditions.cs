using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.MessageStreamModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Conditions;

namespace cccc1808.ProcessEngine.Model.IQueryable.Implementation.MessageStream.Condition
{
    public class MessageStreamConditions<TId, TEntity>
        : IMessageStreamConditions<TId, TEntity>
        where TEntity : IMessageDbEntity<TId>
    {
        private readonly IProcessLinkedConditions<TId, TEntity> _processLinkedConditions;

        public (
            object? _,
            IQueryableCondition<TEntity> Query
            ) IsActiveMessages
        { get; }

        public (
            object? _,
            IQueryableCondition<TEntity, IMessageStreamConditions<TId, TEntity>.ForProcessingParamDto1> Query,
            IQueryableCondition<TEntity, IMessageStreamConditions<TId, TEntity>.ForProcessingParamDto2> QueryIds
            )
            ForProcessing
        { get; }

        public MessageStreamConditions(
            IProcessLinkedConditions<TId, TEntity> processLinkedConditions)
        {
            _processLinkedConditions = processLinkedConditions;

            IsActiveMessages = (
                null,
                new DelegateIQueryableCondition<TEntity>(
                    s => s.Where(e => e.IsActive)
                    )
                );

            ForProcessing = (
                null,
                new DelegateIQueryableCondition<TEntity, IMessageStreamConditions<TId, TEntity>.ForProcessingParamDto1>(
                    (s, p) =>
                    {
                        s = s
                            .ApplayQueryCondition(IsActiveMessages.Query)
                            .OrderByDescending(e => e.Priority)
                            .ThenBy(e => e.OrderId);

                        return s;
                    }
                    ),
                    new DelegateIQueryableCondition<TEntity, IMessageStreamConditions<TId, TEntity>.ForProcessingParamDto2>(
                    (s, p) =>
                    {
                        s = s
                            .ApplayQueryCondition(_processLinkedConditions.ProcessId.QueryRange, p.ProcessIds)
                            .ApplayQueryCondition(IsActiveMessages.Query)
                            .OrderByDescending(e => e.Priority)
                            .ThenBy(e => e.OrderId);

                        return s;
                    }
                    )
                );
        }
    }
}
