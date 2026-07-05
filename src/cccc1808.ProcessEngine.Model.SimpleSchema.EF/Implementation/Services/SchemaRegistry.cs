using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Service;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Services
{
    public class SchemaRegistry : ISchemaRegistry
    {
        private readonly IReadOnlyDictionary<ProcessTypeDto, SchemaProcessRegistrationDto> _processHandlerRegistration;
        private readonly ConcurrentDictionary<ProcessTypeDto, ProcessSchemaDto> _schemaCache;

        public SchemaRegistry(
            IServiceProvider serviceProvider,
            IEnumerable<SchemaProcessRegistrationDto> registrations)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                foreach (var elem in registrations)
                {
                    scope.ServiceProvider.GetRequiredService(elem.ProcessHandlerType);
                }
            }

            _processHandlerRegistration = registrations.ToFrozenDictionary(e => e.ProcessType, e => e);
            _schemaCache = new ConcurrentDictionary<ProcessTypeDto, ProcessSchemaDto>();
        }

        public bool IsSchemaRegistryProcess(ProcessTypeDto processType)
        {
            return _processHandlerRegistration.ContainsKey(processType);
        }

        public bool TryGetSchema(ProcessTypeDto processType, out ProcessSchemaDto schema)
        {
            return _schemaCache.TryGetValue(processType, out schema);
        }

        public bool TryStoreSchema(ProcessTypeDto processType, ProcessSchemaDto schema)
        {
            return _schemaCache.TryAdd(processType, schema);
        }

        public Type GetProcessHandlerType(ProcessTypeDto processType)
        {
            return _processHandlerRegistration[processType].ProcessHandlerType;
        }

        public Type GetProcessStateHandlerType(ProcessTypeDto processType)
        {
            return _processHandlerRegistration[processType].ProcessStateHandlerType;
        }
    }
}
