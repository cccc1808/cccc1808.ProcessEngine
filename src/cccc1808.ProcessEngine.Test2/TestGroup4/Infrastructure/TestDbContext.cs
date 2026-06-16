using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Configuration;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Configuration;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeUpModule.Storage.Configuration;
using cccc1808.ProcessEngine.Model.EfCore.Postgres.Implementation.ProcessModule;
using cccc1808.ProcessEngine.Model.EfCore.Postgres.Implementation.TriggersModule;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Entities;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.ClassifierModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Storage;
using cccc1808.ProcessEngine.Test2.TestGroup4.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Test2.TestGroup4.Infrastructure
{
    public class TestDbContext : DbContext
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _connectionString;

        [Obsolete("Для миграций.")]
        public TestDbContext() 
        {
            _serviceProvider = null!;
            _connectionString = null!;
        }

        public TestDbContext(
            IServiceProvider serviceProvider, 
            string connectionString)
        {
            _connectionString = connectionString;
            _serviceProvider = serviceProvider;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

            optionsBuilder
                .UseNpgsql(_connectionString ?? "")
                .UseSnakeCaseNamingConvention();

            var interceptors = new List<IInterceptor>();

            if (_serviceProvider != null)
            {
                interceptors.Add(
                    new LockQueryHintInterceptor(
                        _serviceProvider.GetRequiredService<ILockQueryHintStore>()
                        )
                    );
            }

            optionsBuilder.AddInterceptors(interceptors);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            {
                modelBuilder.Entity<ProcessDbEntity<Guid>>(
                    b =>
                    {
                        new PostgresProcessDbEntityConfiguration<Guid, ProcessDbEntity<Guid>>().Configure(b);
                        b.Property(e => e.Id).ValueGeneratedNever();
                    });

                modelBuilder.Entity<ProcessErrorDbEntity<Guid>>(
                    b => 
                    {
                        new ProcessErrorConfiguration<Guid>().Configure(b);
                        b.Property(e => e.Id).ValueGeneratedNever();
                    }
                    );

                modelBuilder.Entity<ProcessWakeupDbEntity<Guid>>(
                    b => 
                    {
                        new ProcessWakeUpDbEntityConfiguration<Guid>().Configure(b);
                        b.Property(e => e.Id).ValueGeneratedNever();
                    });

                modelBuilder.Entity<TriggerDbEntity<Guid>>(
                    b => 
                    {
                        new PostgresTriggerDbEntityConfiguration<Guid>().Configure(b);
                        b.Property(e => e.Id).ValueGeneratedNever();
                    });

                modelBuilder.Entity<TriggerEventOutboxDbEntity<Guid>>(
                    b =>
                    {
                        new TriggerEventOutboxDbEntityConfiguration<Guid>().Configure(b);
                        b.Property(e => e.Id).ValueGeneratedNever();
                    });

                modelBuilder.Entity<MemoryJoinStubEntity>();

                // ----------

                modelBuilder.Entity<QueueClassifierDbEntity<Guid>>(
                    b =>
                    {
                        new QueueClassifierDbEntityConfigurator<Guid>().Configure(b);
                    });

                modelBuilder.Entity<AggregateClassifierDbEntity<Guid>>(
                    b =>
                    {
                        new AggregateClassifierDbEntityConfigurator<Guid>().Configure(b);
                    });

                modelBuilder.Entity<InboxProcessDataDbEntity<Guid>>(
                    b => 
                    {
                        new InboxProcessDataConfigurator<Guid>().Configure(b);
                    });

                modelBuilder.Entity<InboxMessageDbEntity<Guid>>(
                    b => 
                    {
                        new InboxMessageConfigurator<Guid>().Configure(b);
                    });

                modelBuilder.Entity<OutboxProcessDataDbEntity<Guid>>(
                    b =>
                    {
                        new OutboxProcessDataConfigurator<Guid>().Configure(b);
                    });

                modelBuilder.Entity<OutboxMessageDbEntity<Guid>>(
                    b =>
                    {
                        new OutboxMessageConfigurator<Guid>().Configure(b);
                    });

                // ----------

                modelBuilder.Entity<BuisnessDbEntity>(
                    b => 
                    {
                        b.Property(e => e.Id).ValueGeneratedNever();
                    });
            }
        }


        public async Task TruncateAllAsync() 
        {
            await Database.ExecuteSqlRawAsync(@"TRUNCATE TABLE ""public"".""trigger_db_entity_guid"" CASCADE", []);
            await Database.ExecuteSqlRawAsync(@"TRUNCATE TABLE ""public"".""process_db_entity_guid"" CASCADE", []);

            await Database.ExecuteSqlRawAsync(@"TRUNCATE TABLE ""public"".""inbox_message_db_entity_guid"" CASCADE", []);
            await Database.ExecuteSqlRawAsync(@"TRUNCATE TABLE ""public"".""inbox_process_data_db_entity_guid"" CASCADE", []);

            await Database.ExecuteSqlRawAsync(@"TRUNCATE TABLE ""public"".""outbox_message_db_entity_guid"" CASCADE", []);            
            await Database.ExecuteSqlRawAsync(@"TRUNCATE TABLE ""public"".""outbox_process_data_db_entity_guid"" CASCADE", []);

            await Database.ExecuteSqlRawAsync(@"TRUNCATE TABLE ""public"".""queue_classifier_db_entity_guid"" CASCADE", []);
            await Database.ExecuteSqlRawAsync(@"TRUNCATE TABLE ""public"".""aggregate_classifier_db_entity_guid"" CASCADE", []);

            await Database.ExecuteSqlRawAsync(@"TRUNCATE TABLE ""public"".""buisness_db_entity"" CASCADE", []);
        }
    }
}
