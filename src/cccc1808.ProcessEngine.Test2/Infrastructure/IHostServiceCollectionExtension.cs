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
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeupModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Limiter;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.WakeupModule.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Test2.Infrastructure
{
    internal static class IHostServiceCollectionExtension
    {
        public static IServiceCollection AddDbServices(this IServiceCollection services)
        {
            services
                .AddScoped<ITransactionManager, EFTransactionManager>()
                .AddScoped<ILockQueryHintStore, LockQueryHintStore>()
                .AddScoped<IEFDbContext, EFDbContext>()
                .AddSingleton<IIdGenerator<Guid>, GuidIdGenerator>();
            return services;
        }

        public static IServiceCollection AddDbServices<TDbContext>(
            this IServiceCollection services,
            Func<IServiceProvider, TDbContext> dbContextFactory)
            where TDbContext : DbContext
        {
            services
                .AddScoped(dbContextFactory)
                .AddScoped<DbContext>(s => s.GetRequiredService<TDbContext>())
                .AddDbServices();

            return services;
        }

        public static IServiceCollection AddIsolationServices(this IServiceCollection services)
        {
            services
                .AddScoped<IIsolationService, EFIsolationService>()
                .AddScoped<ISavepointCompensateService, SavepointCompensateService>()
                .AddScoped<IChangeTrackerCompensateService, EFChangeTrackerCompensateService>()
                .AddScoped<IChangeTrackerSnapshotCompensateService, EFChangeTrackerSnapshotCompensateService>()
                .AddScoped<IManualCompensateService, ManualCompensateService>()
                .AddScoped<IChangeTrackerSnapshotService, ChangeTrackerSnapshotService>()
                ;

            return services;
        }

        public static IServiceCollection AddWakeupServices(
            this IServiceCollection services, 
            params WakeupRegistryDto[] registrations)
        {
            services
                .AddScoped<IWakeupService<Guid>, EFWakeupService<Guid>>()
                .AddSingleton(s => new EFWakeupService<Guid>.OptionsDto())
                .AddSingleton<IWakeupRegistry<Guid>, WakeupRegistry<Guid>>()
                
                .AddScoped<IProcessWakeUpDbEntityConditions<Guid>, ProcessWakeUpDbEntityConditions<Guid>>()
;

            foreach (var elem in registrations)
            {
                services.AddSingleton(elem);
            }

            return services;
        }

        public static IServiceCollection AddProcessServices(
            this IServiceCollection services,
            params ProcessRegistryDto[] registrations)
        {
            services

                .AddScoped<IProcessRepository<Guid>, EFChangeTrackerProcessRepository<Guid, ProcessDbEntity<Guid>>>()                           

                .AddScoped<IProcessSetter>(s => new DefaultProcessSetter((_, _) => DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5)))
                .AddSingleton<IProcessRegistry, ProcessRegistry>()

                .AddScoped<IProcessDbEntityConditions<Guid, ProcessDbEntity<Guid>>, ProcessDbEntityConditions<Guid, ProcessDbEntity<Guid>>>()
                .AddScoped<Id_RangeCondition<Guid, ProcessDbEntity<Guid>>>()
                .AddScoped<IProcessContainerConditions<Guid>, ProcessContainerConditions<Guid>>()
                .AddScoped<IProcessErrorDbEntityConditions<Guid>, ProcessErrorDbEntityConditions<Guid>>()
                ;

            foreach (var elem in registrations)
            {
                services.AddSingleton(elem);
            }

            return services;
        }

        public static IServiceCollection AddProcessExecutionServices(
            this IServiceCollection services,
            LocalProcessBufferService<Guid>.Options localProcessBufferOptions,
            int processCountLimiter) 
        {
            services
                .AddScoped<ILocalProcessBufferService<Guid>, LocalProcessBufferService<Guid>>()
                .AddSingleton(localProcessBufferOptions)
                .AddScoped<IExecuteLimiterInvoker, ExecuteLimiterInvoker>()
                .AddScoped(s => new ProcessCountLimiter(processCountLimiter))
                .AddScoped<IExecuteLimiter>(s => s.GetRequiredService<ProcessCountLimiter>())
                
                .AddScoped<EFProcessSelectQuery<Guid, ProcessDbEntity<Guid>>>()
                .AddSingleton(s => new EFProcessSelectQuery<Guid, ProcessDbEntity<Guid>>.OptionsDto(TimeSpan.FromSeconds(30)))
                ;

            return services;
        }

        public static IServiceCollection AddTriggerServices(
            this IServiceCollection services,
            params TriggerRegistryDto[] registrations) 
        {
            services
                .AddScoped<ITriggerRepository<Guid>, EfTriggerRepository<Guid>>()

                .AddScoped<ITriggerSetter<Guid>, TriggerSetter<Guid>>()
                .AddSingleton<ITriggerHandlerFactory<Guid>, TriggerHandlerFactory<Guid>>()

                .AddScoped<ITriggerDbEntityConditions<Guid>, TriggerDbEntityConditions<Guid>>();

            foreach (var elem in registrations)
            {
                services.AddSingleton(elem);
                services.AddScoped(elem.ImplementationType);
            }

            return services;
        }

        public static IServiceCollection AddTriggerEngineServices(
            this IServiceCollection services,
            TriggerService<Guid>.Options triggerServiceOptions)
        {
            services
                .AddScoped<ITriggerService, TriggerService<Guid>>()
                .AddSingleton(triggerServiceOptions)

                .AddSingleton<ITriggerSelectQuery<Guid>, EFTriggerSelectQuery<Guid>>()
                ;

            return services;
        }
    }
}
