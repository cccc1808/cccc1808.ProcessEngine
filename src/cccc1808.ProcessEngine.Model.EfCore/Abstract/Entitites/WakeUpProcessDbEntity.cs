using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites
{
    public class WakeUpProcessDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }

        public DateTimeOffset TimeStamp { get; set; }

        public bool IsAsyncExecuting { get; set; }

        public DateTimeOffset TimerDate {  get; set; }
    }
}
