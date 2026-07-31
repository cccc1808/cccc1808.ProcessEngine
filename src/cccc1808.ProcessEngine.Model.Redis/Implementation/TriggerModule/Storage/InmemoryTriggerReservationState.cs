using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.Storage
{
    public class InmemoryTriggerReservationState<TId> 
        : ITriggerReservationState<TId>
    {
        private readonly ConcurrentDictionary<TId, DateTimeOffset> _reservations;

        public InmemoryTriggerReservationState()
        {
            _reservations = new ConcurrentDictionary<TId, DateTimeOffset>();
        }

        public ISet<TId> GetAll()
        {
            return _reservations.Keys.ToHashSet();
        }

        public void Reserve(TId processId, DateTimeOffset timeout)
        {
            _reservations.AddOrUpdate(
                processId,
                static (k, p) => p,
                static (k, e, p) => DateTimeOffsetHelper.Max(e, p),
                timeout);
        }

        public void Unreserve(TId procesId)
        {
            _reservations.TryRemove(procesId, out _);
        }

        public void ClearTimeout(DateTimeOffset date)
        {
            foreach (var elem in _reservations)
            {
                if (elem.Value < date)
                {
                    _reservations.TryRemove(elem.Key, out _);
                }
            }
        }

        public void Clear()
        {
            _reservations.Clear();
        }
    }
}
