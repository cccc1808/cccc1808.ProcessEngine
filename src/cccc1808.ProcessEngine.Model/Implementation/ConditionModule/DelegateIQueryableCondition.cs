using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;

namespace cccc1808.ProcessEngine.Model.Implementation.ConditionModule
{
    public class DelegateIQueryableCondition<TData, TParameters> 
        : IQueryableCondition<TData, TParameters>
    {
        private readonly Func<IQueryable<TData>, TParameters, IQueryable<TData>> _applayQueryableFunc;

        public DelegateIQueryableCondition(Func<IQueryable<TData>, TParameters, IQueryable<TData>> applayQueryableFunc)
        {
            _applayQueryableFunc = applayQueryableFunc;
        }

        public IQueryable<TData> ApplayQuery(IQueryable<TData> source, TParameters parameters)
        {
            return _applayQueryableFunc(source, parameters);
        }
    }

    public class DelegateIQueryableCondition<TData>
        : IQueryableCondition<TData>
    {
        private readonly Func<IQueryable<TData>, IQueryable<TData>> _applayQueryableFunc;

        public DelegateIQueryableCondition(Func<IQueryable<TData>, IQueryable<TData>> applayQueryableFunc)
        {
            _applayQueryableFunc = applayQueryableFunc;
        }

        public IQueryable<TData> ApplayQuery(IQueryable<TData> source)
        {
            return _applayQueryableFunc(source);
        }
    }
}
