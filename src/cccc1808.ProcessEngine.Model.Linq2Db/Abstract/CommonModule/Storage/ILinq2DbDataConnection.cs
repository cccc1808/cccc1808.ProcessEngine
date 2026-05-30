using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LinqToDB;
using LinqToDB.Data;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage
{
    public interface ILinq2DbDataConnection
    {
        DataConnection DataConnection { get; }

        ITable<T> Set<T>() 
            where T : class;
    }
}
