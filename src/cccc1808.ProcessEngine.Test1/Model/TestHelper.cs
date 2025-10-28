using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Test1.Model
{
    internal class TestHelper
    {
        public static async Task<Stopwatch> TimerTestAsync(
            Func<Action<int>, Task> body,
            int limit)
        {
            int counter = 0;
            var timer = Stopwatch.StartNew();
            using var waiter = new SemaphoreSlim(1);
            waiter.Wait();

            await body(
                (value) =>
                {
                    counter += value;
                    if (counter >= limit)
                    {
                        timer.Stop();
                        waiter.Release();
                    }
                }
                );

            await waiter.WaitAsync();

            return timer;
        }
    }
}
