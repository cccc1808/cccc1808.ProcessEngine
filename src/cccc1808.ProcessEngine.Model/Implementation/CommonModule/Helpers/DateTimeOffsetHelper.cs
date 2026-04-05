using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers
{
    public static class DateTimeOffsetHelper
    {
        public static DateTimeOffset Min(in DateTimeOffset a, in DateTimeOffset b) 
            => a < b ? a : b;

        public static DateTimeOffset Max(in DateTimeOffset a, in DateTimeOffset b)
            => a > b ? a : b;
    }
}
