using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Entity;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Service;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Services
{
    public class EFSchemaService<TId>
        : ISchemaService<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEFDbContext _dbContext;
        private readonly ISchemaRegistry _schemaRegistry;
        private readonly ISchemaSerializer _schemaSerializer;

        public EFSchemaService(
            IServiceProvider serviceProvider,
            IEFDbContext dbContext,
            ISchemaRegistry schemaRegistry,
            ISchemaSerializer schemaSerializer)
        {
            _serviceProvider = serviceProvider;
            _dbContext = dbContext;
            _schemaRegistry = schemaRegistry;
            _schemaSerializer = schemaSerializer;
        }

        public ISchemaProcessHandler<TId> GetProcessHandler(
            ProcessTypeDto processType)
        {
            var handlerType = _schemaRegistry.GetProcessHandlerType(processType);
            var handler = (ISchemaProcessHandler<TId>)_serviceProvider.GetRequiredService(handlerType);

            return handler;
        }

        public async ValueTask<string> GetSchemaStartTokenId(
            ProcessTypeDto processType, 
            CancellationToken cancellationToken)
        {
            if (!_schemaRegistry.TryGetSchema(processType, out var schema))
            {
                var schemaEntity = await _dbContext.Set<SchemaDbEntity<TId>>()
                    .AsNoTracking()
                    .FirstAsync(e => e.ProcessTypeId == processType.ProcessType && e.ProcessVersion == processType.ProcessVersion);

                schema = _schemaSerializer.Deserialize(schemaEntity.Schema);
                _schemaRegistry.TryStoreSchema(processType, schema);
            }

            return schema.StartTokenId;
        }

        public async ValueTask<TokenDto> GetSchemaToken(
            ProcessTypeDto processType, 
            string tokenId,
            CancellationToken cancellationToken)
        {
            if (!_schemaRegistry.TryGetSchema(processType, out var schema))
            {
                var schemaEntity = await _dbContext.Set<SchemaDbEntity<TId>>()
                    .AsNoTracking()
                    .FirstAsync(e => e.ProcessTypeId == processType.ProcessType && e.ProcessVersion == processType.ProcessVersion);

                schema = _schemaSerializer.Deserialize(schemaEntity.Schema);
                _schemaRegistry.TryStoreSchema(processType, schema);
            }

            return schema.Tokens[tokenId];
        }
    }
}
