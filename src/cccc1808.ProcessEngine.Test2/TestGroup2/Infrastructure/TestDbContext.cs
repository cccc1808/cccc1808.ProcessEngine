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
using cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeUpModule.Storage.Configuration;
using cccc1808.ProcessEngine.Model.EfCore.Postgres.Implementation.ProcessModule;
using cccc1808.ProcessEngine.Model.EfCore.Postgres.Implementation.TriggersModule;

using LinqToDB;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure
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

                modelBuilder.Entity<ProcessWakeUpDbEntity<Guid>>(
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

                modelBuilder.Entity<MemoryJoinStubEntity>();
            }
        }


        public async Task TruncateAllAsync() 
        {
            await Database.ExecuteSqlRawAsync(@"TRUNCATE TABLE ""public"".""trigger_db_entity_guid"" CASCADE", []);
            await Database.ExecuteSqlRawAsync(@"TRUNCATE TABLE ""public"".""process_db_entity_guid"" CASCADE", []);
        }
    }
}
