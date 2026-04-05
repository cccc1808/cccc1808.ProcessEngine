using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Limiter;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Limiter;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Test1.Model.Process1;
using cccc1808.ProcessEngine.Test1.Model.Process1.Storage;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Test1.Model
{
    internal class ModelRegistry
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceCollection"></param>
        /// <param name="options"><see cref="ProcessRunner<Guid>.OptionsDto"/></param>
        /// <param name="selectFactory"></param>
        /// <param name="rootMiddlewareFactory">Фабрика корневого middleware обработки.</param>
        /// <param name="bufferLimit">Ограничение размера InMemory очереди.</param>
        /// <param name="processCountLimiter"><see cref="ProcessCountLimiter"/></param>
        /// <param name="dbPort"></param>
        /// <param name="useMemory">Применить настройку буфера к Postgres connectio string.</param>
        /// <param name="useLockQueryHint"><see cref="ILockQueryHintStore"/></param>
        public static void Registry(
            IServiceCollection serviceCollection,
            ProcessRunner<Guid>.OptionsDto options,
            Func<IServiceProvider, IProcessAsyncProcessingSelectQuery<Guid>> selectFactory,
            Func<IServiceProvider, IProcessHandlerMiddleware<Guid>> rootMiddlewareFactory,
            int bufferLimit,
            int processCountLimiter,
            int dbPort,
            bool useMemory,
            bool useLockQueryHint)
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
                    connectionString: connectionString,
                    useLockQueryHint: useLockQueryHint
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
                .AddScoped<IManualCompensateService, ManualCompensateService>();

            serviceCollection
                .AddScoped<IProcessSetter>(
                    s => new DefaultProcessSetter(retryDelayFunc: null))
                .AddSingleton<IProcessRegistry, ProcessRegistry>()
                ;
        }

        public static Func<IServiceProvider, IProcessAsyncProcessingSelectQuery<Guid>> Selector1(
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
