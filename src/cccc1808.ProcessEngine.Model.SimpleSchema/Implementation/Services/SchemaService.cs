using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Handlers;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Service;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Service.Serializers;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Services
{
    public class SchemaService<TId>
        : ISchemaService<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IQueries _queries;
        private readonly ISchemaRegistry _schemaRegistry;
        private readonly ISchemaSerializer _schemaSerializer;

        public SchemaService(
            IServiceProvider serviceProvider,
            IQueries queries,
            ISchemaRegistry schemaRegistry,
            ISchemaSerializer schemaSerializer)
        {
            _serviceProvider = serviceProvider;
            _queries = queries;
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

        public ISchemaProcessStateHandler<TId> GetProcessStateHandler(ProcessTypeDto processType)
        {
            var handlerType = _schemaRegistry.GetProcessStateHandlerType(processType);
            var handler = (ISchemaProcessStateHandler<TId>)_serviceProvider.GetRequiredService(handlerType);

            return handler;
        }

        public async ValueTask<string> GetSchemaStartTokenId(
            ProcessTypeDto processType, 
            CancellationToken cancellationToken)
        {
            var schema = await GetProcessSchemaAsync(processType, cancellationToken);
            return schema.StartTokenId;
        }

        public async ValueTask<TokenDto> GetSchemaToken(
            ProcessTypeDto processType, 
            string tokenId,
            CancellationToken cancellationToken)
        {
            var schema = await GetProcessSchemaAsync(processType, cancellationToken);
            return schema.Tokens[tokenId];
        }

        private async ValueTask<ProcessSchemaDto> GetProcessSchemaAsync(
            ProcessTypeDto processType,
            CancellationToken cancellationToken) 
        {
            if (!_schemaRegistry.TryGetSchema(processType, out var schema))
            {
                var schemaJson = await _queries.GetSchemaAsync(processType, cancellationToken);

                schema = _schemaSerializer.Deserialize(schemaJson);
                _schemaRegistry.TryStoreSchema(processType, schema);
            }

            return schema;
        }

        #region types

        public interface IQueries
        {
            Task<JsonElement> GetSchemaAsync(
                ProcessTypeDto processType, 
                CancellationToken cancellationToken);
        }

        #endregion
    }
}
