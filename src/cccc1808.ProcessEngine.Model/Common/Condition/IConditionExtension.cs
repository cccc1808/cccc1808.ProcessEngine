using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Common.Condition
{
    public static class IConditionExtension
    {
        public static IQueryable<TEntity> ApplayFilterCondition<TEntity, TParameter>(
            this IQueryable<TEntity> source,
            IQueryableCondition<TEntity, TParameter> condition,
            TParameter parameter)
        {
            return condition.ApplayQueryable(
                source,
                parameter);
        }

        public static IEnumerable<TEntity> ApplayFilterCondition<TEntity, TParameter>(
            this IEnumerable<TEntity> source,
            IInMemoryCondition<TEntity, TParameter> condition,
            TParameter parameter)
        {
            return condition.ApplayEnumerable(
                source,
                parameter);
        }

        public static IEnumerable<TTarget> ApplayProjectionCondition<TEntity, TTarget>(
            this IEnumerable<TEntity> source,
            IInMemoryProjectionCondition<TEntity, TTarget> condition)
        {
            return condition.ApplayProjectionEnumerable(
                source);
        }
    }
}
