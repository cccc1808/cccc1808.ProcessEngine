using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule
{
    public class DateTimeProvider 
        : IDateTimeProvider
    {
        public DateTimeOffset UtcNow 
            => DateTimeOffset.UtcNow;
    }
}
