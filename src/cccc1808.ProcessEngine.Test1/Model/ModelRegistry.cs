using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Abstract.Services.Limiter;
using cccc1808.ProcessEngine.Model.Abstract.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Abstract.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.Storage;
using cccc1808.ProcessEngine.Model.Abstract.Storage.Query;
using cccc1808.ProcessEngine.Model.Common.QueryHint;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Implementation;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Services;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Storage;
using cccc1808.ProcessEngine.Model.EntityFrameworkCore.Implementation.Query;
using cccc1808.ProcessEngine.Model.Implementation;
using cccc1808.ProcessEngine.Model.Implementation.Compensate;
using cccc1808.ProcessEngine.Model.Implementation.Limiter;
using cccc1808.ProcessEngine.Model.Implementation.Runners;
using cccc1808.ProcessEngine.Model.Implementation.Setter;
using cccc1808.ProcessEngine.Model.Implementation.Storage;
using cccc1808.ProcessEngine.Test1.Model.Process1;
using cccc1808.ProcessEngine.Test1.Model.Process1.Storage;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Test1.Model
{
    internal class ModelRegistry
    {
        public static void Registry(
            IServiceCollection serviceCollection,
            ProcessRunner<Guid>.OptionsDto options,
            Func<IServiceProvider, IProcessSelectQuery<Guid>> selectFactory,
            Func<IServiceProvider, IProcessHandlerMiddleware<Guid>> rootMiddlewareFactory,
            int bufferLimit,
            int processCountLimiter,
            int dbPort,
            bool useMemory)
        {
            serviceCollection
                .AddSingleton<ILocalProcessBufferService<Guid>>(
                    s => new LocalProcessBufferService<Guid>(
                        sizeLimit: bufferLimit))
                .AddTransient<IProcessRunner>(
                    s => new ProcessRunner<Guid>(
                        s,
                        options,
                        s.GetRequiredService<ILocalProcessBufferService<Guid>>(),
                        s.GetRequiredService<IExecuteLimiterInvoker>(),
                        s.GetRequiredService<ProcessCountLimiter>(),
                        selectFactory,
                        rootMiddlewareFactory
                        ))
                    .AddScoped<Handler1>()
                    .AddScoped<Handler2>()
                    ;

            serviceCollection
                .AddSingleton<IExecuteLimiterInvoker, ExecuteLimiterInvoker>()
                .AddSingleton(s => new ProcessCountLimiter(limit: processCountLimiter))
                .AddSingleton<IExecuteLimiter>(s => s.GetRequiredService<ProcessCountLimiter>());

            
            var readBufferSize = 524288; // 0,5 МБ        
            var writeBufferSize = 524288; // 0,5 МБ

            var connectionString = useMemory                        
                ? $"Host=localhost;Port={dbPort};Database=test;Username=postgres;Password=postgres; Read Buffer Size={readBufferSize};Write Buffer Size={writeBufferSize}"                        
                : $"Host=localhost;Port={dbPort};Database=test;Username=postgres;Password=postgres;";

            serviceCollection
                .AddScoped<AppDbContext>(s => new AppDbContext(
                    s.GetRequiredService<IServiceProvider>(), 
                    connectionString
                    )
                    )
                .AddScoped<ITransactionManager, EFTransactionManager>()
                .AddScoped<ILockQueryHintStore, LockQueryHintStore>()
                .AddScoped<IChangeTrackerSnapshotService, ChangeTrackerSnapshotService>()
                .AddScoped<Process1Repository>()
                .AddScoped<IProcessDbProvider < Guid > , Process1DataDbProvider>()
                .AddScoped<IEFDbContext>(s => new EFDbContext(s.GetRequiredService<AppDbContext>()))

                .AddScoped<IIsolationService, EFIsolationService>()
                .AddScoped<ISavepointCompensateService, SavepointCompensateService>()
                .AddScoped<IChangeTrackerCompensateService, EFChangeTrackerCompensateService>()
                .AddScoped<IChangeTrackerSnapshotCompensateService, EFChangeTrackerSnapshotCompensateService>()
                .AddScoped<IManualCompensateService, ManualCompensateService>()
                
                .AddScoped<IWakeUpService<Guid>, EFWakeUpService<Guid>>();

            serviceCollection
                .AddScoped<IProcessSetter>(
                    s => new DefaultProcessSetter(retryDelayFunc: null))
                .AddSingleton<IProcessRegistry, ProcessRegistry>()
                ;
        }

        public static Func<IServiceProvider, IProcessSelectQuery<Guid>> Selector1(
            TimeSpan lockDelay)
        {
            return (s) => new EFProcessSelectQuery<Guid, ProcessDbEntity<Guid>>(
                new EFProcessSelectQuery<Guid, ProcessDbEntity<Guid>>.OptionsDto(
                    SelectoLockDelay: lockDelay),
                s.GetRequiredService<IEFDbContext>(),
                s.GetRequiredService<ITransactionManager>(),
                s.GetRequiredService<ILockQueryHintStore>(),
                s.GetRequiredService<IProcessDbEntityConditions<Guid, ProcessDbEntity<Guid>>>()
                );
        }
    }
}
