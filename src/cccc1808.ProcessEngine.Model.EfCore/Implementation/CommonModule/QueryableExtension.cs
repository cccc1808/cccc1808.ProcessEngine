using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule
{
    public static class QueryableExtension
    {
        // Добавляем "продвинутый" вариант Where
        public static IQueryable<T> DWhere<T, TProjection>(
            this IQueryable<T> queryable,
            Expression<Func<T, TProjection>> prop,
            Expression<Func<TProjection, bool>> where)
        {
            return queryable.Where(prop.Compose(where));
        }

        // Фактически мы реализуем композицию выражений,
        // которая даст нам выражение, соответствующее композиции целевых функций
        private static Expression<Func<TIn, TOut>> Compose<TIn, TInOut, TOut>(                
            this Expression<Func<TIn, TInOut>> input,               
            Expression<Func<TInOut, TOut>> inOutOut)
        {
            // это параметр x => blah-blah. Для лямбды нам нужен null
            var param = Expression.Parameter(typeof(TIn), null);
            // получаем объект, к которому применяется выражение
            var invoke = Expression.Invoke(input, param);
            // и выполняем "получи объект и примени к нему его выражение"
            var res = Expression.Invoke(inOutOut, invoke);

            // возвращаем лямбду нужного типа
            return Expression.Lambda<Func<TIn, TOut>>(res, param);
        }        
    }
}
