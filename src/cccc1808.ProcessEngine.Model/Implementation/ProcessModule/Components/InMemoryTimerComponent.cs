using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Components
{
    public class InMemoryTimerComponent : ITimerComponent
    {
        private readonly Dictionary<string, DateTimeOffset> _data;

        public InMemoryTimerComponent(Dictionary<string, DateTimeOffset> data)
        {
            _data = data;
        }

        public void CreateTimer(string key, DateTimeOffset date)
        {
            _data.Add(key, date);
        }

        public void RemoveTimer(string key)
        {
            _data.Remove(key);
        }

        public bool TryGetActivatedTimers(
            DateTimeOffset now,
            out IDictionary<string, DateTimeOffset> timers)
        {
            timers = _data
                .Where(e => e.Value > now)
                .ToDictionary();
            return timers.Any();
        }

        public bool TryGetTimer(string key, out DateTimeOffset date)
        {
            return _data.TryGetValue(key, out date);
        }
    }
}
