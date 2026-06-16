using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Entity;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Service;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Components;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Storage.DbProviders
{
    public class EFSchemaProcessDataDbEntityDbProvider<TId> 
        : IProcessDbProvider<TId>
    {
        private readonly IEFDbContext _dbContext;
        private readonly ISchemaRegistry _schemaRegistry;
        private readonly IActionStateSerializer _actionStateSerializer;

        public EFSchemaProcessDataDbEntityDbProvider(
            IEFDbContext dbContext,
            ISchemaRegistry schemaRegistry,
            IActionStateSerializer actionStateSerializer)
        {
            _dbContext = dbContext;
            _schemaRegistry = schemaRegistry;
            _actionStateSerializer = actionStateSerializer;
        }

        public async Task LoadProcessDataAsync(
            IDictionary<TId, IProcessContainer<TId>> processes, 
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex,
            bool isAsyncExecution, 
            CancellationToken cancellationToken)
        {
            var currentProcesses = byTypeIndex
                .Where(e => _schemaRegistry.IsSchemaRegistryProcess(e.Key))
                .SelectMany(e => e.Value)
                .Select(e => processes[e])
                .ToArray();

            if (!currentProcesses.Any())
            {
                return;
            }

            var dbData =  await _dbContext.Set<SchemaProcessDataDbEntity<TId>>()
                .Where(e => currentProcesses.Select(e => e.Id).Contains(e.ProcessId))
                .ToDictionaryAsync(e => e.ProcessId, e => e, cancellationToken);

            foreach (var elem in currentProcesses)
            {
                var entity = dbData[elem.Id];

                {                    
                    var component = new EFSchemaProcessComponentProxy<TId>(
                        entity,
                        _actionStateSerializer.Deserialize(entity.ActionState));
                    elem.AddComponent<ISchemaProcessComponent>(component);
                }                
            }
        }

        public Task UpdateAsync(
            IDictionary<TId, IProcessContainer<TId>> processes, 
            IDictionary<ProcessTypeDto, ICollection<TId>> byTypeIndex, 
            CancellationToken cancellationToken)
        {
            var currentProcesses = byTypeIndex
                .Where(e => _schemaRegistry.IsSchemaRegistryProcess(e.Key))
                .SelectMany(e => e.Value)
                .Select(e => processes[e])
                .ToArray();

            foreach (var elem in currentProcesses)
            {
                var component = (EFSchemaProcessComponentProxy<TId>)elem.GetComponent<ISchemaProcessComponent>();
                component.Entity.ActionState = _actionStateSerializer.Serialize(component.AllActionStates());
            }

            return Task.CompletedTask;
        }
    }
}
