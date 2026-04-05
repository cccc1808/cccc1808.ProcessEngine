using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;

namespace cccc1808.ProcessEngine.Test1.Test
{
    [TestClass]
    public class TimeoutTest
    {
        [TestMethod]
        public async Task Test1Async()
        {
            await TimeoutHelper.ExecuteWithTimeoutAsync(
                1, 
                TimeSpan.FromSeconds(1),
                async (_, _) => await Task.Delay(TimeSpan.FromHours(1)),
                default);
        }
    }
}
