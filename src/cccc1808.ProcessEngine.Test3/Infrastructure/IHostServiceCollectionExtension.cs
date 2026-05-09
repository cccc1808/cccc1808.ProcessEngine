using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
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
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Limiter;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Implementation.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.WakeupModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Implementation.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Implementation.WakeUpModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Kafka.Implementation.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Configuration;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Linq2Db.Implementation.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Linq2Db.Implementation.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Linq2Db.Implementation.ProcessModule.Storage.Configuration;
using cccc1808.ProcessEngine.Model.Linq2Db.Implementation.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Linq2Db.Implementation.TriggersModule.Storage.Configuration;
using cccc1808.ProcessEngine.Model.Linq2Db.Implementation.TriggersModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Linq2Db.Implementation.WakeUpModule.Storage.Configuration;
using cccc1808.ProcessEngine.Model.Linq2Db.Implementation.WakeUpModule.Storage.Queries;
using cccc1808.ProcessEngine.Test.Common;

using LinqToDB.Data;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Test2.Infrastructure
{
    internal static class IHostServiceCollectionExtension
    {
        /// <summary>
        /// Для текущих тестов достаточно InMemory. Уменьшает время выполнения тестов.
        /// Выключить, если нужна првоерка на реальном брокере.
        /// </summary>
        private static bool UseInMemoryQueue => true;

        public static void RegistryDbConfiguration<TEntity, TConfigurator, TInitMigration>(IServiceCollection services)
            where TConfigurator : class, ILinq2DbConfigurator<TEntity>
            where TInitMigration : class, ILinq2DbMigration
        {
            services
                .AddScoped<TConfigurator>()
                .AddScoped<ILinq2DbConfigurator>(s => s.GetRequiredService<TConfigurator>())
                .AddScoped<TInitMigration>()
                .AddScoped<ILinq2DbMigration>(s => s.GetRequiredService<TInitMigration>());
        }

        public static IServiceCollection AddDbServices(
            this IServiceCollection services,
            Action configureTables,
            params Type[] dbProviders)
        {
            services
                .AddScoped<ITransactionManager, Linq2DbTransactionManager>()
                .AddScoped<ILockQueryHintStore, LockQueryHintStore>()
                .AddScoped<ILinq2DbDataConnection, Linq2DbDataConnection>()
                .AddSingleton<IIdGenerator<Guid>, GuidIdGenerator>();

            foreach (var elem in dbProviders)
            {
                services.AddScoped(elem);
                services.AddScoped<IProcessDbProvider<Guid>>(s => (IProcessDbProvider<Guid>)s.GetRequiredService(elem));
            }            

            RegistryDbConfiguration<
                ProcessDbEntity<Guid>,
                ProcessDbEntityConfiguration<Guid, ProcessDbEntity<Guid>>,
                ProcessDbEntityInitMigration<Guid>
                    >(services);

            RegistryDbConfiguration<
                ProcessErrorDbEntity<Guid>,
                ProcessErrorConfiguration<Guid>,
                ProcessErrorInitMigration<Guid>
                >(services);

            RegistryDbConfiguration<
                TriggerDbEntity<Guid>,
                TriggerDbEntityConfiguration<Guid>,
                TriggerDbEntityInitMigration<Guid>
                >(services);

            RegistryDbConfiguration<
                ProcessWakeupDbEntity<Guid>,
                ProcessWakeupDbEntityConfiguration<Guid>,
                ProcessWakeupDbEntityInitMigration<Guid>
                >(services);

            configureTables();

            return services;
        }

        public static IServiceCollection AddDbServices<TDataConnection>(
            this IServiceCollection services,
            Func<IServiceProvider, TDataConnection> dbContextFactory,
            Func<IServiceProvider, Linq2DbMigrator.OptionsDto> migratorOptionsFactory,
            Action configureTables,
            params Type[] dbProviders)
            where TDataConnection : DataConnection
        {
            services
                .AddScoped(dbContextFactory)
                .AddScoped<DataConnection>(s => s.GetRequiredService<TDataConnection>())                
                .AddDbServices(configureTables, dbProviders);

            services
                .AddScoped<ILinq2DbMigrator, Linq2DbMigrator>()
                .AddSingleton(s => migratorOptionsFactory(s))
                .AddSingleton<Linq2DbMigrator.MappingSchemaContainer>();

            return services;
        }

        public static IServiceCollection AddKafkaServices(
            this IServiceCollection services,
            KafkaQueueProviderFactory.OptionsDto options)
        {
            services.AddSingleton(options);

            if (!UseInMemoryQueue)
            {
                services.AddSingleton<IQueueProviderFactory, KafkaQueueProviderFactory>();
            }
            else
            {
                services.AddSingleton<IQueueProviderFactory, TestInMemoryQueueProviderFactory>();
            }

            return services;
        }

        public static IServiceCollection AddIsolationServices(this IServiceCollection services)
        {
            services
                .AddScoped<IIsolationService, Linq2DbIsolationService>()
                .AddScoped<ISavepointCompensateService, SavepointCompensateService>()
                .AddScoped<IManualCompensateService, ManualCompensateService>()
                ;

            return services;
        }

        public static IServiceCollection AddWakeupServices(
            this IServiceCollection services, 
            WakeupRegistryDto[] wakeupRegistrations,
            StreamRegistryDto[] streamRegistrations)
        {
            services
                .AddScoped<IWakeupService<Guid>, WakeupService<Guid>>()
                .AddSingleton(s => new WakeupService<Guid>.OptionsDto())
                .AddScoped<IWakeupServiceQueries<Guid>, Linq2DbWakeupServiceQueries<Guid>>()
                .AddSingleton<IWakeupRegistry<Guid>, WakeupRegistry<Guid>>()
                
                .AddScoped<IProcessWakeupDbEntityConditions<Guid>, ProcessWakeupDbEntityConditions<Guid>>();

            foreach (var elem in wakeupRegistrations)
            {
                services.AddSingleton(elem);
                services.AddScoped(elem.CheckWakeupHandlerType);
            }

            foreach (var elem in streamRegistrations)
            {
                services.AddSingleton(elem);
            }

            return services;
        }

        public static IServiceCollection AddProcessServices(
            this IServiceCollection services,
            Linq2DbProcessRepository<Guid, ProcessDbEntity<Guid>>.Options repositoryOptions,
            params ProcessRegistryDto[] registrations)
        {
            services

                .AddSingleton<IDateTimeProvider, DateTimeProvider>()

                .AddScoped<IProcessRepository<Guid>, Linq2DbProcessRepository<Guid, ProcessDbEntity<Guid>>>()
                .AddSingleton(repositoryOptions)

                .AddScoped<IProcessSetter>(s => new DefaultProcessSetter(
                    s.GetRequiredService<IDateTimeProvider>(),
                    (_, _) => DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5)
                    )                
                )
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
                
                .AddScoped<Linq2DbProcessSelectQuery<Guid, ProcessDbEntity<Guid>>>()
                .AddSingleton(s => new Linq2DbProcessSelectQuery<Guid, ProcessDbEntity<Guid>>.OptionsDto(TimeSpan.FromSeconds(30)))
                ;

            return services;
        }

        public static IServiceCollection AddTriggerServices(
            this IServiceCollection services,
            params TriggerRegistryDto[] registrations) 
        {
            services
                .AddScoped<ITriggerRepository<Guid>, Linq2DbTriggerRepository<Guid>>()

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
            TriggerRunner<Guid>.OptionsDto triggerServiceOptions,
            TriggerOptions<Guid> triggerOptions)
        {
            services
                .AddScoped<ITriggerRunner, TriggerRunner<Guid>>()
                .AddSingleton(triggerServiceOptions)

                .AddScoped<ITriggerSelectQuery<Guid>, Linq2DbTriggerSelectQuery<Guid>>()

                .AddScoped<ITriggerEventRaiser<Guid>, TriggerEventRaiser<Guid>>()
                .Decorate<ITriggerEventRaiser<Guid>, TriggerEventRaiserAfterTransactionCompleteDecorator<Guid>>()
                .AddSingleton(triggerOptions)
                .AddScoped<IEventJsonSerializer, EventJsonSerializer<Guid>>()
                ;

            return services;
        }
    }
}
