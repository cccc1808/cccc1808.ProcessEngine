using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;

namespace cccc1808.ProcessEngine.Model.Implementation.ConditionModule
{
    public class DelegateInMemoryCondition<TData>
        : IInMemoryCondition<TData>
    {
        private readonly Func<TData, bool> _checkFunc;
        private readonly Func<IEnumerable<TData>, IEnumerable<TData>> _applayEnumerableFunc;

        public DelegateInMemoryCondition(
            Func<TData, bool> checkFunc,
            Func<IEnumerable<TData>, IEnumerable<TData>>? applayEnumerableFunc = null)
        {
            _checkFunc = checkFunc;
            _applayEnumerableFunc = applayEnumerableFunc
                ?? ((source) => source.Where(Check));
        }

        public bool Check(TData source)
        {
            return _checkFunc(source);
        }

        public IEnumerable<TData> ApplayEnumerable(IEnumerable<TData> source)
        {
            return _applayEnumerableFunc(source);
        }
    }

    public class DelegateInMemoryCondition<TData, TParameters> 
        : IInMemoryCondition<TData, TParameters>
    {
        private readonly Func<TData, TParameters, bool> _checkFunc;
        private readonly Func<IEnumerable<TData>, TParameters, IEnumerable<TData>> _applayEnumerableFunc;        

        public DelegateInMemoryCondition(
            Func<TData, TParameters, bool> checkFunc,
            Func<IEnumerable<TData>, TParameters, IEnumerable<TData>>? applayEnumerableFunc = null)
        {
            _checkFunc = checkFunc;
            _applayEnumerableFunc = applayEnumerableFunc 
                ?? ((source, parameter) => source.Where(e => Check(e, parameter)));            
        }

        public bool Check(TData source, TParameters parameters)
        {
            return _checkFunc(source, parameters);
        }

        public IEnumerable<TData> ApplayEnumerable(IEnumerable<TData> source, TParameters parameters)
        {
            return _applayEnumerableFunc(source, parameters);
        }        
    }
}
