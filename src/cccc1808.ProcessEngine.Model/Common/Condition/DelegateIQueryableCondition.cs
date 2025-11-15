namespace cccc1808.ProcessEngine.Model.Common.Condition
{
    public class DelegateIQueryableCondition<TData, TParameters> 
        : IQueryableCondition<TData, TParameters>
    {
        private readonly Func<IQueryable<TData>, TParameters, IQueryable<TData>> _applayQueryableFunc;

        public DelegateIQueryableCondition(Func<IQueryable<TData>, TParameters, IQueryable<TData>> applayQueryableFunc)
        {
            _applayQueryableFunc = applayQueryableFunc;
        }

        public IQueryable<TData> ApplayQueryable(IQueryable<TData> source, TParameters parameters)
        {
            return _applayQueryableFunc(source, parameters);
        }
    }
}
