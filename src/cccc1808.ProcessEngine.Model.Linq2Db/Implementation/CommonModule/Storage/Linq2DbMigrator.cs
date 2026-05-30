using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Configuration;

using LinqToDB.Data;
using LinqToDB.Mapping;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.CommonModule.Storage
{
    public class Linq2DbMigrator : ILinq2DbMigrator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEnumerable<ILinq2DbConfigurator> _configurators;
        private readonly IEnumerable<ILinq2DbMigration> _migrations;
        private readonly MappingSchemaContainer _mappingSchemaContainer;
        private readonly OptionsDto _options;

        public Linq2DbMigrator(
            IServiceProvider serviceProvider,
            IEnumerable<ILinq2DbConfigurator> configurators,
            IEnumerable<ILinq2DbMigration> migrations,
            MappingSchemaContainer mappingSchemaContainer,
            OptionsDto options)
        {
            _serviceProvider = serviceProvider;
            _configurators = configurators;
            _migrations = migrations;
            _mappingSchemaContainer = mappingSchemaContainer;
            _options = options;
        }

        public void ConfigureSchema() 
        {
            var builder = new FluentMappingBuilder(_mappingSchemaContainer.MappingSchema);

            _mappingSchemaContainer.MappingSchema.AddMetadataReader(new SnakeCaseNamingConventionMetadataReader());

            foreach (var elem in _configurators)
            {
                elem.Configure(builder);
            }

            builder.Build();
        }

        public async Task MigrateAsync(CancellationToken cancellationToken)
        {
            {
                using (var connection = new NpgsqlConnection(_options.ConnectionString))
                {
                    await connection.OpenAsync();
                    await using (var command = new NpgsqlCommand($"CREATE DATABASE {_options.DatabaseName}", connection))
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }

            {
                var transactionManager = _serviceProvider.GetRequiredService<ITransactionManager>();
                var dataConnection = _serviceProvider.GetRequiredService<DataConnection>();
                await using var transaction = await transactionManager.StartTransactionAsync(cancellationToken);

                foreach (var elem in _migrations)
                {
                    await elem.MigrateAsync(dataConnection, cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
        }

        public class MappingSchemaContainer 
        {
            public MappingSchema MappingSchema { get; }
                = new MappingSchema();
        }

        public class OptionsDto 
        {
            public string? ConnectionString { get; set; }

            public string? DatabaseName { get; set; }
        }
    }
}
