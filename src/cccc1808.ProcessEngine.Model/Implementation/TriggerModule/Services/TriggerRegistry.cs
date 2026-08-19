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
    public class TriggerRegistry
        : ITriggerRegistry
    {        
        private readonly FrozenDictionary<TriggerTypeUniqueDto, TriggerRegistryDto> _registrations;
        private readonly FrozenDictionary<string, Type> _handlerMapping;

        public TriggerRegistry(
            IEnumerable<TriggerRegistryDto> registrations)
        {
            var buffer1 = new Dictionary<TriggerTypeUniqueDto, TriggerRegistryDto>();
            var buffer2 = new Dictionary<string, Type>();

            foreach (var elem in registrations)
            {
                buffer1.Add(elem.Unique, elem);
                
                if (!buffer2.TryGetValue(elem.Unique.HandlerName, out var implementationType))
                {
                    buffer2.Add(elem.Unique.HandlerName, elem.Metadata.ImplementationType);
                }
                else if (implementationType != elem.Metadata.ImplementationType)
                {
                    throw new InvalidOperationException($"Недопускается регистрация разных хендлеров триггеров у одного ключа. {nameof(TriggerRegistryDto)}");
                }                    
            }

            _registrations = buffer1.ToFrozenDictionary();
            _handlerMapping = buffer2.ToFrozenDictionary();
        }

        public IReadOnlyCollection<TriggerRegistryDto> GetAll()
        {
            return _registrations.Values;
        }

        public Type GetHandlerType(string handler)
        {
            return _handlerMapping[handler];
        } 

        public bool TryGetHandlerType(string handler, out Type handlerType)
        {
            if (_handlerMapping.TryGetValue(handler, out var registration))
            {
                handlerType = registration;
                return true;
            }

            handlerType = null!;
            return false;
        }
    }
}
