using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services
{
    public class TriggerRegistry : ITriggerRegistry
    {
        private readonly FrozenDictionary<string, TriggerRegistryDto> _registrations;

        public TriggerRegistry(
            IEnumerable<TriggerRegistryDto> registrations)
        {
            _registrations = registrations.ToFrozenDictionary(e => e.HandlerName, e => e);
        }

        public IReadOnlyCollection<TriggerRegistryDto> GetAll()
        {
            return _registrations.Values;
        }

        public Type GetHandlerType(string handler)
        {
            return _registrations[handler].ImplementationType;
        }

        public bool TryGetHandlerType(string handler, out Type handlerType)
        {
            if (_registrations.TryGetValue(handler, out var registration))
            {
                handlerType = registration.ImplementationType;
                return true;
            }

            handlerType = null!;
            return false;
        }
    }
}
