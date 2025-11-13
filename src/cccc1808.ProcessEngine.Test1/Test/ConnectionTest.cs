using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Storage;
using cccc1808.ProcessEngine.Model.Common.QueryHint;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Storage;
using cccc1808.ProcessEngine.Model.Implementation.Storage;
using cccc1808.ProcessEngine.Test1.Model;
using cccc1808.ProcessEngine.Test1.Model.Process1;
using cccc1808.ProcessEngine.Test1.Model.Process1.Storage;

using Docker.DotNet.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.PostgreSql;

namespace cccc1808.ProcessEngine.Test1.Test
{
    [TestClass]
    public class ConnectionTest
    {
        private static bool RandomPort => true;

        [TestMethod]
        public async Task DbTest()
        {
            PostgreSqlContainer postgreSqlContainer;
            IServiceProvider serviceProvider;
            {
                var postgresBuilder = new PostgreSqlBuilder()
                    .WithImage("postgres:18");

                postgresBuilder = RandomPort
                    ? postgresBuilder
                    : postgresBuilder.WithPortBinding(15433, PostgreSqlBuilder.PostgreSqlPort);

                postgreSqlContainer = postgresBuilder.Build();
                await postgreSqlContainer.StartAsync();

                await new DbInit().InitAllAsync(postgreSqlContainer, useDbOptimizations: true);

                var services = new ServiceCollection();
                services
                   .AddScoped<AppDbContext>(s => new AppDbContext(
                       s.GetRequiredService<IServiceProvider>(),
                       $"Host=localhost;Port={postgreSqlContainer.GetMappedPublicPort()};Database=test;Username=postgres;Password=postgres"))
                   .AddScoped<ITransactionManager, EFTransactionManager>()
                   .AddScoped<ILockQueryHintStore, LockQueryHintStore>()
                   .AddScoped<Process1Repository>();

                serviceProvider = services.BuildServiceProvider();
            }

            try 
            {
                await using (var scope = serviceProvider.CreateAsyncScope())
                {
                    var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var repo = scope.ServiceProvider.GetRequiredService<Process1Repository>();

                    await repo.CreateAsync(0, 0, default);
                    await appDbContext.SaveChangesAsync();
                }
            }
            finally 
            {
                await postgreSqlContainer.DisposeAsync();
            }            
        }

        [TestMethod]
        public async Task FillTest()
        {
            PostgreSqlContainer postgreSqlContainer;
            IServiceProvider serviceProvider;
            {
                var postgresBuilder = new PostgreSqlBuilder()
                    .WithImage("postgres:18");

                postgresBuilder = RandomPort
                    ? postgresBuilder
                    : postgresBuilder.WithPortBinding(15433, PostgreSqlBuilder.PostgreSqlPort);

                postgreSqlContainer = postgresBuilder.Build();
                await postgreSqlContainer.StartAsync();

                await new DbInit().InitAllAsync(postgreSqlContainer, useDbOptimizations: true);

                var services = new ServiceCollection();
                services
                   .AddScoped<AppDbContext>(s => new AppDbContext(
                       s.GetRequiredService<IServiceProvider>(),
                       $"Host=localhost;Port={postgreSqlContainer.GetMappedPublicPort()};Database=test;Username=postgres;Password=postgres"))
                   .AddScoped<ITransactionManager, EFTransactionManager>()
                   .AddScoped<ILockQueryHintStore, LockQueryHintStore>()
                   .AddScoped<Process1Repository>();

                serviceProvider = services.BuildServiceProvider();
            }

            await using (var scope = serviceProvider.CreateAsyncScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<Process1Repository>();
                var appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                for (var q = 0; q < 100; q++) 
                {
                    for (var i = 0; i < 10000; i++)
                    {
                        await repo.CreateAsync(
                            Random.Shared.Next(10000),
                            (short)Random.Shared.Next(10000),
                            default);
                    }
                    await appDbContext.SaveChangesAsync();
                    appDbContext.ChangeTracker.Clear();
                }                
            }
        }
    }
}
