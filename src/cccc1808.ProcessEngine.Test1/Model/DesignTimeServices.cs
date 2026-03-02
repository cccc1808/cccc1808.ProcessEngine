using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.QueryHint;
using cccc1808.ProcessEngine.Model.Implementation.Storage;
using cccc1808.ProcessEngine.Test1.Model.Process1;

using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Test1.Model
{
    /// <summary>
    /// Для миграции
    /// </summary>
    public class DesignTimeServices : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var services = new ServiceCollection();

            services
                   .AddScoped<AppDbContext>(s => new AppDbContext(
                       s.GetRequiredService<IServiceProvider>(),
                       connectionString: $"Host=localhost;Port={15433};Database=test;Username=postgres;Password=postgres",
                       useLockQueryHint: true))
                   .AddScoped<ILockQueryHintStore, LockQueryHintStore>();

            return services.BuildServiceProvider()
                .CreateAsyncScope()
                .ServiceProvider
                .GetRequiredService<AppDbContext>();
        }
    }
}
