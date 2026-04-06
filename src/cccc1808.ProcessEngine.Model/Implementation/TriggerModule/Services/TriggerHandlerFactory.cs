using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services
{
    public class TriggerHandlerFactory<TId> : ITriggerHandlerFactory<TId>
    {
        private readonly IReadOnlyDictionary<string, Type> _registrations;

        public TriggerHandlerFactory(
            IServiceProvider serviceProvider,
            IEnumerable<TriggerRegistryDto> registrations)
        {
            foreach (var elem in registrations)
            {
                var handler = (ITriggerHandler)serviceProvider
                    .GetRequiredService(elem.ImplementationType);
            }

            _registrations = registrations.ToDictionary(
                e => e.Key, 
                e => e.ImplementationType);
        }        

        public ITriggerHandler GetHandler(
            IServiceProvider serviceProvider,
            string key)
        {
            return (ITriggerHandler)serviceProvider
                .GetRequiredService(_registrations[key]);
        }
    }
}
