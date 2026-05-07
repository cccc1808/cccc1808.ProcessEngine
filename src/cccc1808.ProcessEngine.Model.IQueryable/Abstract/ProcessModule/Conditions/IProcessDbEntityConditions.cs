using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Conditions
{
    public interface IProcessDbEntityConditions<TId, TEntity>
        where TEntity : ProcessDbEntity<TId>
    {
        (
            // IInMemoryProjectionCondition<TEntity, TId> Projection,
            IInMemoryCondition<TEntity, ICollection<TId>> MemoryRange,
            IQueryableCondition<TEntity, ICollection<TId>> QueryRange
            ) Id
        { get; }

        (
            object? no,
            IQueryableCondition<TEntity, DbProcessingForSelectorParameters> Query
            ) DbProcessingForSelector
        { get; }

        (
            object? no,
            IQueryableCondition<TEntity, DbProcessingForHandlerParameters> Query
            ) DbProcessingForHandler
        { get; }

        /// <summary>
        /// Условие асинхронной обработки.
        /// </summary>
        (
            IInMemoryCondition<TEntity> Memory,
            IQueryableCondition<TEntity> Query,
            IQueryableCondition<TEntity, ICollection<TId>> QueryIds
            ) AsyncExecute
        { get; }

        /// <summary>
        /// Условие ожидания.
        /// </summary>
        (
            IInMemoryCondition<TEntity> Memory,
            IQueryableCondition<TEntity> Query,
            IQueryableCondition<TEntity, ICollection<TId>> QueryIds
            ) WaitEvent
        { get; }

        /// <summary>
        /// Процесс возможно завис из-за потери TriggerEvent.
        /// Используется для страхующего воркера перепроверяющего необходимость пробуждения процесса.
        /// </summary>
        (
            object? _no,
            IQueryableCondition<TEntity, DateTimeOffset> QueryRange
            ) MaybeStoppedByTriggerEventLoosed
        { get; }


        public readonly record struct DbProcessingForSelectorParameters(
            DateTimeOffset now,
            ICollection<ProcessRegistryDto> registrations);

        public readonly record struct DbProcessingForHandlerParameters(
            ICollection<ProcessRegistryDto> registrations,
            ICollection<TId> ids);
    }
}
