using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule.Extensions
{
    public static class HashSetExternsion
    {
        public static void AddRange<T>(
            this HashSet<T> set,
            ICollection<T> values)
        {
            var requiredCapacity = set.Count + values.Count;
            set.EnsureCapacity(requiredCapacity);

            foreach (var elem in values)
            {
                set.Add(elem);
            }
        }

        public static void AddRange<T, TSource>(
            this HashSet<T> set,
            ICollection<TSource> values,
            Func<TSource, T> selector)
        {
            var requiredCapacity = set.Count + values.Count;
            set.EnsureCapacity(requiredCapacity);

            foreach (var elem in values)
            {
                set.Add(selector(elem));
            }
        }
    }
}
