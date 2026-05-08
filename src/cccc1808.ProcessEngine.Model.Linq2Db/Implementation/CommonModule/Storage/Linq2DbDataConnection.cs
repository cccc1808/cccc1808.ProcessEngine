using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;

using LinqToDB;
using LinqToDB.Data;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.CommonModule.Storage
{
    public class Linq2DbDataConnection 
        : ILinq2DbDataConnection
    {
        public DataConnection DataConnection { get; }

        public Linq2DbDataConnection(DataConnection dataConnection)
        {
            DataConnection = dataConnection;
        }

        public ITable<T> Set<T>()
            where T : class => DataConnection.GetTable<T>();
    }
}
