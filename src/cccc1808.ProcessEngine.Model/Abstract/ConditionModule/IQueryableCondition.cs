namespace cccc1808.ProcessEngine.Model.Abstract.ConditionModule
{
    /// <summary>
    /// Фиксация выборки IQueryable.
    /// </summary>
    public interface IQueryableCondition<TData, TParameters>
    {
        /// <summary>
        /// Фильтрация для БД.
        /// Может использовать специфичные для БД функции.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        IQueryable<TData> ApplayQuery(IQueryable<TData> source, TParameters parameters);
    }

    /// <summary>
    /// Фиксация выборки IQueryable.
    /// </summary>
    public interface IQueryableCondition<TData>
    {
        /// <summary>
        /// Фильтрация для БД.
        /// Может использовать специфичные для БД функции.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        IQueryable<TData> ApplayQuery(IQueryable<TData> source);
    }
}
