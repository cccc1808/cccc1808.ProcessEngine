namespace cccc1808.ProcessEngine.Model.Abstract.Common.Condition
{
    public interface IQueryableCondition<TData, TParameters>
    {
        /// <summary>
        /// Фильтрация для БД.
        /// Может использовать специфичные для БД функции.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        IQueryable<TData> ApplayQueryable(IQueryable<TData> source, TParameters parameters);
    }
}
