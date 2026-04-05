using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;

namespace cccc1808.ProcessEngine.Model.Implementation.ConditionModule
{
    public static class ConditionExtension
    {
        public static IEnumerable<TEntity> ApplayInMemoryCondition<TEntity>(
            this IEnumerable<TEntity> source,
            IInMemoryCondition<TEntity> condition)
        {
            return condition.ApplayEnumerable(
                source);
        }

        public static IEnumerable<TEntity> ApplayInMemoryCondition<TEntity, TParameter>(
            this IEnumerable<TEntity> source,
            IInMemoryCondition<TEntity, TParameter> condition,
            TParameter parameter)
        {
            return condition.ApplayEnumerable(
                source,
                parameter);
        }

        public static IQueryable<TEntity> ApplayQueryCondition<TEntity>(
            this IQueryable<TEntity> source,
            IQueryableCondition<TEntity> condition)
        {
            return condition.ApplayQuery(
                source);
        }

        public static IQueryable<TEntity> ApplayQueryCondition<TEntity, TParameter>(
            this IQueryable<TEntity> source,
            IQueryableCondition<TEntity, TParameter> condition,
            TParameter parameter)
        {
            return condition.ApplayQuery(
                source,
                parameter);
        }        

        //public static IEnumerable<TTarget> ApplayProjectionCondition<TEntity, TTarget>(
        //    this IEnumerable<TEntity> source,
        //    IInMemoryProjectionCondition<TEntity, TTarget> condition)
        //{
        //    return condition.ApplayProjectionEnumerable(
        //        source);
        //}
    }
}
