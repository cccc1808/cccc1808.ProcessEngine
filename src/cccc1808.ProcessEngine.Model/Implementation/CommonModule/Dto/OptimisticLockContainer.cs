using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto
{
    /// <summary>
    /// CAS pattern.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class OptimisticLockContainer<T>
        where T : class
    {
        private object _value;
        
        public T Data => (T)_value;

        public OptimisticLockContainer(T data)
        {
            _value = data;
        }

        public T TryUpdate<TParameter>(
            TParameter parameter,
            Func<TParameter, T, T> tryFunc,
            CancellationToken cancellationToken)
        {
            while(true)
            {
                var oldValue = _value;
                var oldValueTyped = (T)oldValue;

                var newValueTyped = tryFunc(parameter, oldValueTyped);

                // Атомарно пытаемся заменить старое значение на новое
                var result = Interlocked.CompareExchange(ref _value, newValueTyped, oldValue);

                // Если CAS сработала (result == oldValue), значит, никто не изменил значение до нас
                if (ReferenceEquals(result, oldValue))
                {
                    return newValueTyped;
                }

            }
        }

        public (T, TResult) TryUpdate<TParameter, TResult>(
            TParameter parameter,
            Func<TParameter, T, (T, TResult)> tryFunc,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                var oldValue = _value;
                var oldValueTyped = (T)oldValue;

                var newValueTyped = tryFunc(parameter, oldValueTyped);

                // Атомарно пытаемся заменить старое значение на новое
                var result = Interlocked.CompareExchange(ref _value, newValueTyped.Item1, oldValue);

                // Если CAS сработала (result == oldValue), значит, никто не изменил значение до нас
                if (ReferenceEquals(result, oldValue))
                {
                    return newValueTyped;
                }
            }
        }
    }
}
