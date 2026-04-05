using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Test1.Model.Process1
{
    public class AppDbContext : DbContext
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _connectionString;
        private readonly bool _useLockQueryHintStore;

        public AppDbContext(
            IServiceProvider serviceProvider,
            string connectionString,
            bool useLockQueryHint)
        {
            _serviceProvider = serviceProvider;
            _connectionString = connectionString;
            _useLockQueryHintStore = useLockQueryHint;            
        }

        [Obsolete("Для MemoryJoin.")]
        private DbSet<MemoryJoinStubEntity> MemoryJoin => Set<MemoryJoinStubEntity>();

        public DbSet<ProcessDbEntity<Guid>> Process => Set<ProcessDbEntity<Guid>>();

        public DbSet<Process1DataDbEntity> Process1Datas => Set<Process1DataDbEntity>();


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

            optionsBuilder
                .UseNpgsql(_connectionString)
                .UseSnakeCaseNamingConvention();

            var interceptors = new List<IInterceptor>();

            if (_useLockQueryHintStore)
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
                modelBuilder.Entity<ProcessTypeEntity>(
                    b =>
                    {
                        b.HasKey(e => e.Id);
                        b.Property(e => e.Id).ValueGeneratedNever();

                        b.Property(e => e.Name)
                            .HasMaxLength(255);

                        b.HasIndex(e => new { e.Name, e.Version })
                            .IncludeProperties(e => e.Id)
                            .IsUnique();

                        b.HasData(
                            new ProcessTypeEntity()
                            {
                                Id = 0,
                                Name = "Process1",
                                Version = 0
                            }
                            );
                    });

                modelBuilder.Entity<ProcessErrorDbEntity<Guid>>(
                    b =>
                    {
                        b.HasKey(e => e.Id);
                        b.Property(e => e.Id).ValueGeneratedNever();

                        b.HasOne(e => e.Process)
                            .WithOne(e => e.Error)
                            .HasForeignKey<ProcessErrorDbEntity<Guid>>(e => e.ProcessId)
                            .OnDelete(DeleteBehavior.Cascade);
                    });

                modelBuilder.Entity<ProcessDbEntity<Guid>>(
                    b =>
                    {
                        b.HasKey(e => e.Id);
                        b.Property(e => e.Id).ValueGeneratedNever();

                        // Для загрузки для обработчика.
                        b.HasIndex(e => e.Id)
                            .HasFilter($"status = {(int)ProcessStatusEnum.AsyncExecute}");

                        // Для выборки в очередь.
                        b.HasIndex(e => new { e.Priority, e.ProcessTypeId, e.ProcessVersion, e.SelectLockTimeout })
                            .IncludeProperties(e => e.Id)
                            .HasFilter($"status = {(int)ProcessStatusEnum.AsyncExecute}");

                        // Для работы страхующего триггера
                        b.HasIndex(e => new { e.ProcessTypeId, e.ProcessVersion, e.Status, e.SelectLockTimeout })
                            .HasFilter($"status = {(int)ProcessStatusEnum.WaitEvent} and have_error_flag is false and re_try_count is null");
                    });
            }

            {
                modelBuilder.Entity<InboxProcessDataDbEntity<Guid>>(
                    b => 
                    {
                        b.HasIndex(e => e.ProcessId)
                            .IncludeProperties(e => e.Id)
                            .IsUnique();
                    });
                modelBuilder.Entity<InboxMessageDbEntity<Guid>>(
                    b => 
                    {
                        b.HasIndex(e => new { e.ProcessId, e.IdemporencyId })
                            .IsUnique();

                        b.HasIndex(e => new { e.ProcessId, e.OrderId })
                            .IsUnique();

                        b.HasIndex(e => new { e.ProcessId, e.Priority, e.OrderId })
                            .IncludeProperties(e => e.Id)
                            .HasFilter("is_active is true");
                    });

                modelBuilder.Entity<OutboxProcessDataDbEntity<Guid>>(
                    b => 
                    {
                        b.HasIndex(e => e.ProcessId)
                            .IncludeProperties(e => e.Id)
                            .IsUnique();
                    });
                modelBuilder.Entity<OutboxMessageDbEntity<Guid>>(
                    b => 
                    {
                        b.HasIndex(e => new { e.ProcessId, e.IdemporencyId })
                            .IsUnique();

                        b.HasIndex(e => new { e.ProcessId, e.OrderId })
                            .IsUnique();

                        b.HasIndex(e => new { e.ProcessId, e.Priority, e.OrderId })
                            .IncludeProperties(e => e.Id)
                            .HasFilter("is_active is true");
                    });
            }

            {
                modelBuilder.Entity<Process1DataDbEntity>(
                    b =>
                    {
                        b.HasKey(e => e.Id);
                        b.Property(e => e.Id).ValueGeneratedNever();

                        b.HasOne(e => e.Process)
                            .WithOne()
                            .HasForeignKey<Process1DataDbEntity>(e => e.ProcessId)
                            .IsRequired(true)
                            .OnDelete(DeleteBehavior.Cascade);
                    });
            }
        }
    }
}
