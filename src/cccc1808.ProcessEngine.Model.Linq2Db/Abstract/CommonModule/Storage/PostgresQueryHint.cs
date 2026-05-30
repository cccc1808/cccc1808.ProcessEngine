using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage
{
    public static class PostgresQueryHint
    {
        public static string ForNoKeyUpdateSkipLocked => "FOR NO KEY UPDATE SKIP LOCKED";

        public static string ForNoKeyUpdate => "FOR NO KEY UPDATE";

        public static string ForShare => "FOR SHARE";
    }
}
