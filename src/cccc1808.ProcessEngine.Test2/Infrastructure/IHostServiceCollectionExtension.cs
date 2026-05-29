using System;
using System.Collections.Generic;
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
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeupModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeUpModule.Storage.Queries;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Limiter;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Implementation.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.CommonModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Conditions;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.ClassifierModule.Conditions;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.ClassifierModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.CommonModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.InboxModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.OutboxModule.Services;
using cccc1808.ProcessEngine.Model.Kafka.Implementation.QueueModule.Provider;
using cccc1808.ProcessEngine.Test2.Infrastructure.Queue;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Scrutor;

namespace cccc1808.ProcessEngine.Test2.Infrastructure
{
    internal static class IHostServiceCollectionExtension
    {
        /// <summary>
        /// Для текущих тестов достаточно InMemory. Уменьшает время выполнения тестов.
        /// Выключить, если нужна првоерка на реальном брокере.
        /// </summary>
        private static bool UseInMemoryQueue => true;

        public static IServiceCollection AddDbServices(
            this IServiceCollection services,
            params Type[] dbProviders)
        {
            services
                .AddScoped<ITransactionManager, EFTransactionManager>()
                .AddScoped<ILockQueryHintStore, LockQueryHintStore>()
                .AddScoped<IEFDbContext, EFDbContext>()
                .AddSingleton<IIdGenerator<Guid>, GuidIdGenerator>();

            foreach (var elem in dbProviders)
            {
                services.AddScoped(elem);
                services.AddScoped<IProcessDbProvider<Guid>>(s => (IProcessDbProvider<Guid>)s.GetRequiredService(elem));
            }

            return services;
        }

        public static IServiceCollection AddDbServices<TDbContext>(
            this IServiceCollection services,
            Func<IServiceProvider, TDbContext> dbContextFactory,
            params Type[] dbProviders)
            where TDbContext : DbContext
        {
            services
                .AddScoped(dbContextFactory)
                .AddScoped<DbContext>(s => s.GetRequiredService<TDbContext>())
                .AddDbServices(dbProviders);            

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
                .AddScoped<IIsolationService, EFIsolationService>()
                .AddScoped<ISavepointCompensateService, SavepointCompensateService>()
                .AddScoped<IChangeTrackerCompensateService, EFChangeTrackerCompensateService>()
                .AddScoped<IChangeTrackerSnapshotCompensateService, EFChangeTrackerSnapshotCompensateService>()
                .AddScoped<INoIsolationCompensateService, NoIsolationCompensateService>()
                .AddScoped<IChangeTrackerSnapshotService, ChangeTrackerSnapshotService>()
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
                .AddScoped<IWakeupServiceQueries<Guid>, EFWakeupServiceQueries<Guid>>()
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
            EFChangeTrackerProcessRepository<Guid, ProcessDbEntity<Guid>>.Options repositoryOptions,
            params ProcessRegistryDto[] registrations)
        {
            services

                .AddSingleton<IDateTimeProvider, DateTimeProvider>()

                .AddScoped<IProcessRepository<Guid>, EFChangeTrackerProcessRepository<Guid, ProcessDbEntity<Guid>>>()
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
                .AddScoped<ITriggerSetter<Guid>.IOneOfSetter, TriggerSetter<Guid>.OneOfSetterImpl>()
                .AddScoped<ITriggerSetter<Guid>.IStandartSetter, TriggerSetter<Guid>.StandartSetterImpl>()
                .AddScoped<ITriggerSetter<Guid>.ICounterSetter, TriggerSetter<Guid>.CounterSetterImpl>()
                .AddScoped<ITriggerSetter<Guid>.ISimpleStreamSetter, TriggerSetter<Guid>.SimpleStreamSetterImpl>()                
                .AddSingleton(
                    new TriggerSetter<Guid>.SimpleStreamSetterImpl.OptionsDto() 
                    {
                        NoCounterOptimization = true,
                    }
                    )
                .AddScoped<ITriggerSetter<Guid>.IOffsetStreamSetter, TriggerSetter<Guid>.OffsetStreamSetterImpl>()
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

                .AddScoped<ITriggerSelectQuery<Guid>, EFTriggerSelectQuery<Guid>>()

                .AddScoped<ITriggerEventRaiser<Guid>, TriggerEventRaiser<Guid>>()
                .Decorate<ITriggerEventRaiser<Guid>, TriggerEventRaiserAfterTransactionCompleteDecorator<Guid>>()
                .AddSingleton(triggerOptions)
                .AddScoped<IEventJsonSerializer, EventJsonSerializer<Guid>>()

                .AddScoped<IRootTriggerService<Guid>, RootTriggerService<Guid>>()
                .AddScoped<IRootTriggerService<Guid>.IQueries, EFRootTriggerServiceQueries<Guid>>()
                .AddSingleton(
                    new RootTriggerService<Guid>.OptionsDto(
                        triggerServiceOptions.TriggerEventQueues.First().QueueName,
                        triggerServiceOptions.TriggerEventQueues.First().QueueName
                        )
                    )
                ;

            return services;
        }

        public static IServiceCollection AddInboxOutbox(
            this IServiceCollection services,
            InboxRunner<Guid>.OptionsDto inboxRunnerOptions,
            EFInboxConsumerService<Guid>.Options inboxConsumerOptions,
            EFInboxDbProvider<Guid>.Options inboxDbProviderOptions,
            EFOutboxDbProvider<Guid>.Options outboxDbProviderOptions,
            InboxRegistryDto inboxRegistry,
            OutboxRegistryDto outboxRegistry) 
        {
            services

                .AddScoped<IOutboxSender<Guid>, EFOutboxSender<Guid>>()

                .AddScoped<EFInboxDbProvider<Guid>>()
                .AddSingleton(inboxDbProviderOptions)
                .AddScoped<IProcessDbProvider<Guid>>(s => s.GetRequiredService<EFInboxDbProvider<Guid>>())
                .AddScoped<EFOutboxDbProvider<Guid>>()
                .AddSingleton(outboxDbProviderOptions)
                .AddScoped<IProcessDbProvider<Guid>>(s => s.GetRequiredService<EFOutboxDbProvider<Guid>>())

                .AddScoped<IHeaderJsonSerializer, HeaderJsonSerializer>()

                .AddScoped<IInboxSetter, InboxSetter>()
                .AddScoped<IOutboxSetter, OutboxSetter>()

                .AddScoped<IInboxRunner, InboxRunner<Guid>>()
                .AddSingleton(inboxRunnerOptions)

                .AddScoped<IInboxConsumerService, EFInboxConsumerService<Guid>>()
                .AddSingleton(inboxConsumerOptions)

                .AddScoped<IClassifierRepository<Guid>, EFClassifierRepository<Guid>>()
                .AddSingleton<EFClassifierRepository<Guid>.CachState>()

                .AddScoped(s => new OutboxRangeProcessHandler<Guid>(
                    s.GetRequiredService<IProcessRepository<Guid>>(),
                    s.GetRequiredService<ITriggerRepository<Guid>>(),
                    s.GetRequiredService<IProcessSetter>(),
                    s.GetRequiredService<IQueueProviderFactory>(),
                    s.GetRequiredService<IDateTimeProvider>(),
                    s.GetRequiredService<IOutboxSetter>(),
                    s.GetRequiredService<IHeaderJsonSerializer>(),
                    new ExecuteStepByStepGroupMiddleware<Guid>.OptionsDto(
                        10,
                        IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker,
                        true,
                        true,
                        true)
                    ))

                .AddScoped<IAggregateClassifierDbEntityCondition<Guid>, AggregateClassifierDbEntityCondition<Guid>>()

                .AddSingleton(inboxRegistry)
                .AddSingleton(outboxRegistry)

                .AddScoped<IProcessLinkedConditions<Guid, InboxMessageDbEntity<Guid>>, ProcessLinkedConditions<Guid, InboxMessageDbEntity<Guid>>>()
                .AddScoped<IMessageStreamConditions<Guid, InboxMessageDbEntity<Guid>>, MessageStreamConditions<Guid, InboxMessageDbEntity<Guid>>>()
                .AddScoped<IProcessLinkedConditions<Guid, OutboxMessageDbEntity<Guid>>, ProcessLinkedConditions<Guid, OutboxMessageDbEntity<Guid>>>()
                .AddScoped<IMessageStreamConditions<Guid, OutboxMessageDbEntity<Guid>>, MessageStreamConditions<Guid, OutboxMessageDbEntity<Guid>>>()

                .AddScoped<IProcessLinkedConditions<Guid, InboxProcessDataDbEntity<Guid>>, ProcessLinkedConditions<Guid, InboxProcessDataDbEntity<Guid>>>()
                .AddScoped<IProcessLinkedConditions<Guid, OutboxProcessDataDbEntity<Guid>>, ProcessLinkedConditions<Guid, OutboxProcessDataDbEntity<Guid>>>()
                ;

            return services;
        }
    }
}
