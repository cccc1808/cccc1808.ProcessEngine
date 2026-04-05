using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Test1.Model.Process1;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.PostgreSql;

namespace cccc1808.ProcessEngine.Test1.Model
{
    internal class DbInit
    {
        public async Task InitAllAsync(
            PostgreSqlContainer postgreSqlContainer,
            bool useDbOptimizations)
        {
            ServiceProvider serviceProvider;
            {
                var serviceCollection = new ServiceCollection();

                serviceCollection
                    .AddScoped<ILockQueryHintStore, LockQueryHintStore>()
                    .AddScoped<AppDbContext>(s => new AppDbContext(
                        s.GetRequiredService<IServiceProvider>(),
                        connectionString: $"Host=localhost;Port={postgreSqlContainer.GetMappedPublicPort()};Database=test;Username=postgres;Password=postgres;",
                        useLockQueryHint: true
                    )
                    );

                serviceProvider = serviceCollection.BuildServiceProvider();
            }

            await using (var scope = serviceProvider.CreateAsyncScope())
            {
                var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await appDbContext.Database.EnsureCreatedAsync();
                await InitAsync(postgreSqlContainer, appDbContext, useDbOptimizations);
            }

            await serviceProvider.DisposeAsync();
        }

        public async Task InitAsync(
            PostgreSqlContainer container,
            AppDbContext dbContext,
            bool useDbOptimizations)
        {
            await dbContext.Database.ExecuteSqlRawAsync("VACUUM FULL;");

            await InitTablesAsync(dbContext);

            if (useDbOptimizations)
            {
                await InitOptionsAsync(container, dbContext);
            }
        }

        public async Task InitTablesAsync(
            AppDbContext dbContext)
        {
            static void Table(StringBuilder buider, string name)
            {
                buider.AppendLine(@$"
    ALTER TABLE {name} SET (
        fillfactor = 50,
        autovacuum_vacuum_cost_delay=5, 
        autovacuum_vacuum_cost_limit=500,
        autovacuum_vacuum_scale_factor=0.0001
    );");
            }
            static void Index(StringBuilder buider, string name)
            {
                buider.AppendLine(@$"
    ALTER INDEX {name} SET (fillfactor=50);
    REINDEX INDEX {name};");
            }
            static void ProcessTable(
                StringBuilder builder, 
                IEntityType entityType) 
            {
                Table(builder, entityType.GetTableName());
                foreach (var elem in entityType.GetIndexes())
                {
                    Index(builder, elem.GetDatabaseName());
                }
                Index(builder, $"pk_{entityType.GetTableName()}");
            }

            var builder = new StringBuilder();

            ProcessTable(builder, dbContext.Model.FindEntityType(typeof(ProcessDbEntity<Guid>)));
            ProcessTable(builder, dbContext.Model.FindEntityType(typeof(ProcessErrorDbEntity<Guid>)));
            ProcessTable(builder, dbContext.Model.FindEntityType(typeof(Process1DataDbEntity)));

            //Table(builder, "process_error_db_entity_guid");
            //Table(builder, "process");
            //Table(builder, "process1datas");
            //Index(builder, "pk_process");
            //Index(builder, "ix_process_priority_process_type_id_version_select_lock");
            //Index(builder, "pk_process1datas");

            var query = builder.ToString();

            await dbContext.Database.ExecuteSqlRawAsync(query);
            
        }

        public async Task InitOptionsAsync(
            PostgreSqlContainer container,
            AppDbContext dbContext) 
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
ALTER SYSTEM SET wal_buffers = '64MB';
ALTER SYSTEM SET shared_buffers = '512MB';
ALTER SYSTEM SET work_mem = '24MB';
ALTER SYSTEM SET wal_buffers = '16MB';
ALTER SYSTEM SET random_page_cost = 1.1;
ALTER SYSTEM SET effective_io_concurrency = 32;

");

            // Перезапускаем контейнер, чтобы применить настройки.
            await container.StopAsync();
            await container.StartAsync();

            // var q = dbContext.Database.SqlQueryRaw<string>(@"show max_connections;");
        }
    }
}
