using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers
{
    public static class TimespanHelper
    {
        public static TimeSpan Max(in TimeSpan a, in TimeSpan b)
            => a > b ? a : b;

        public static TimeSpan Min(in TimeSpan a, in TimeSpan b)
            => a < b ? a : b;
    }
}
