using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.ExternalCounter;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Storage.ExternalCounter
{
    public class InMemoryExternalCounterProvider 
        : IExternalCounterProvider
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _members;
        private readonly ConcurrentDictionary<string, int> _counters;        

        public InMemoryExternalCounterProvider()
        {
            _members = new ConcurrentDictionary<string, ConcurrentDictionary<string, bool>>();
            _counters = new ConcurrentDictionary<string, int>();
        }

        public Task CreateCounterAsync(
            string triggerKey,
            int value,
            CancellationToken cancellationToken)
        {
            _counters[triggerKey] = value;
            _members[triggerKey] = new ConcurrentDictionary<string, bool>();

            return Task.CompletedTask;
        }

        public Task RemoveCounterAsync(string triggerKey, CancellationToken cancellationToken)
        {
            _counters.TryRemove(triggerKey, out _);
            _members.TryRemove(triggerKey, out _);

            return Task.CompletedTask;
        }

        public Task<bool> CounterExists(string triggerKey, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                _counters.ContainsKey(triggerKey) && _members.ContainsKey(triggerKey)
                );
        }

        public Task<bool> CheckDecrementedAsync(string triggerKey, string processId)
        {
            if (!_members.TryGetValue(triggerKey, out var triggerMembers))
            {
                throw new Exception("Counter not found exception");
            }

            if (!triggerMembers.TryRemove(processId, out _))
            {
                return Task.FromResult(false);
            }

            _counters.AddOrUpdate(
                triggerKey, 
                (e) => throw new Exception("CounterNotFound"), 
                (k, e) => e + 1);

            return Task.FromResult(true);
        }

        public Task<int> TryDecrementCounterAsync(string triggerKey, string processId)
        {
            if (!_members.TryGetValue(triggerKey, out var triggerMembers))
            {
                throw new Exception("Counter not found exception");
            }

            var isInserted = triggerMembers.AddOrUpdate(processId, true, (_, _) => false);

            if (!isInserted)
            {
                return Task.FromResult(_counters[triggerKey]);
            }

            var counterValue = _counters.AddOrUpdate(
                triggerKey, 
                (_) => throw new Exception("Counter not found exception"), 
                (k, e) => e - 1);

            return Task.FromResult(counterValue);
        }

        public Task DecrementCompleteAsync(string triggerKey, string processId)
        {
            if (!_members.TryGetValue(triggerKey, out var triggerMembers))
            {
                throw new Exception("Counter not found exception");
            }

            triggerMembers.TryRemove(processId, out _);
            return Task.CompletedTask;
        }

        public Task<Dictionary<string, (int Counter, ISet<string> Members)>> GetCountersByTriggersAsync(
            ICollection<string> triggersKeys,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, (int Counter, ISet<string> Members)>(triggersKeys.Count);

            foreach (var elem in triggersKeys)
            {
                if (_counters.TryGetValue(elem, out var counter) && _members.TryGetValue(elem, out var triggerMembers))
                {
                    result.Add(
                        elem, 
                        (counter, triggerMembers.Keys.ToHashSet()));
                }
            }

            return Task.FromResult(result);
        }        

        public Task ClearAsync()
        {
            _counters.Clear();
            _members.Clear();

            return Task.CompletedTask;
        }
    }
}
