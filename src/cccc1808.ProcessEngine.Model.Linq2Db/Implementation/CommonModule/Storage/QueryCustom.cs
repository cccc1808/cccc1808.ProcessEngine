using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

using LinqToDB;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.CommonModule.Storage
{
    public static class QueryCustom
    {
        [Sql.Expression("{0} > {1}", PreferServerSide = true)]
        public static bool Linq2DbCompare<T>(this T x, T value)
            => throw new NotSupportedException("Db expression");
    }
}
