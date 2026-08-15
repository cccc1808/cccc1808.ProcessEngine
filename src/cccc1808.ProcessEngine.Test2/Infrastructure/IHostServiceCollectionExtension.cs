using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Provider;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Storage.ExternalCounter;
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
using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;
using cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule.Queue;
using cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.Queue;
using cccc1808.ProcessEngine.Model.Redis.Implementation.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Redis.Implementation.ProcessModule.Storage.Queue;
using cccc1808.ProcessEngine.Model.Redis.Implementation.ProcessModule.Storage.Reserve;
using cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.Storage.Queue;
using cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.Storage.Reserve;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Service;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Service;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Service.Serializers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Services;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Storage.Queries;
using cccc1808.ProcessEngine.Model.SimpleSchema.Implementation.Handlers;
using cccc1808.ProcessEngine.Model.SimpleSchema.Implementation.Services;
using cccc1808.ProcessEngine.Model.SimpleSchema.Implementation.Services.Serializers;
using cccc1808.ProcessEngine.Test2.Infrastructure.Queue;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace cccc1808.ProcessEngine.Test2.Infrastructure
{
    internal static class IHostServiceCollectionExtension
    {
        /// <summary>
        /// Для текущих тестов достаточно InMemory. Уменьшает время выполнения тестов.
        /// Выключить, если нужна првоерка на реальном брокере.
        /// </summary>
        private static bool UseInMemoryQueue => true;

        private static bool UseInMemoryExternalCounter => false;

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

        public static IServiceCollection AddQueueProcessRunner(
            this IServiceCollection services, 
            QueueProcessRunner<Guid>.OptionsDto options)
        {
            services
                .AddScoped<IQueueProcessRunnerQuery<Guid>, EFQueueProcessRunnerQuery<Guid>>()
                .AddScoped<IQueueProcessRunner, QueueProcessRunner<Guid>>()
                .AddSingleton(options);

            return services;
        }

        public static IServiceCollection AddRedisProcessQueueServices(
            this IServiceCollection services,

            RedisProcessReserveProvider<Guid>.OptionsDto reserveOptions,
            ProcessQueueOptionsDto<Guid> processQueueOptions
            )
        {
            services
                .AddScoped<IProcessReserveProvider<Guid>, RedisProcessReserveProvider<Guid>>()
                .AddSingleton(reserveOptions)

                .AddScoped<IProcessQueueProvider<Guid>, RedisProcessQueueProvider<Guid>>()
                .AddSingleton(processQueueOptions)

                .AddSingleton<IRedisProcessQueueNotifyState, RedisProcessQueueNotifyState>()

                .AddScoped<IProcessQueueContext<Guid>, ProcessQueueContext<Guid>>()

                .AddScoped<IRedisProcessQueueNotificationRunner, RedisProcessQueueNotificationRunner<Guid>>()
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
                .AddScoped<ITriggerSetter<Guid>.IOneOfTriggerSetter, TriggerSetter<Guid>.OneOfTriggerSetterImpl>()
                .AddScoped<ITriggerSetter<Guid>.IOneOfTriggerEventSetter, TriggerSetter<Guid>.OneOfTriggerEventSetterImpl>()
                .AddScoped<ITriggerSetter<Guid>.IStandartSetter, TriggerSetter<Guid>.StandartSetterImpl>()
                .AddScoped<ITriggerSetter<Guid>.IChildTriggerSetter, TriggerSetter<Guid>.ChildTriggerSetterImpl>()
                .AddScoped<ITriggerSetter<Guid>.ICounterSetter, TriggerSetter<Guid>.CounterSetterImpl>()
                .AddScoped<ITriggerSetter<Guid>.IStreamSetter, TriggerSetter<Guid>.StreamSetterImpl>()
                .AddScoped<ITriggerSetter<Guid>.ISimpleStreamSetter, TriggerSetter<Guid>.SimpleStreamSetterImpl>()
                .AddSingleton(
                    new TriggerSetter<Guid>.SimpleStreamSetterImpl.OptionsDto()
                    {
                        NoCounterOptimization = true,
                    }
                    )
                .AddScoped<ITriggerSetter<Guid>.IOffsetStreamSetter, TriggerSetter<Guid>.OffsetStreamSetterImpl>()
                .AddSingleton<ITriggerHandlerFactory<Guid>, TriggerHandlerFactory<Guid>>()

                .AddScoped<EFTriggerHandlerFacade<Guid>>()
                .AddScoped<ITriggerHandlerFacade<Guid>>(s => s.GetRequiredService<EFTriggerHandlerFacade<Guid>>())
                .AddScoped<EmergencyTriggerHandler<Guid>.IQueries, EFEmergencyTriggerHandlerQueries<Guid>>()
                .AddScoped<IRootTriggerQuery<Guid>, StubRootTriggerQuery<Guid>>()

                .AddScoped<ITriggerComponentCondition<Guid>, TriggerComponentCondition<Guid>>()
                .AddScoped<ITriggerDbEntityConditions<Guid>, TriggerDbEntityConditions<Guid>>()                
                ;

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
            TriggerOptions<Guid> triggerOptions,
            RedisTriggerQueueOptionsDto<Guid> triggerQueueOptions,
            RedisTriggerReserveProvider<Guid>.OptionsDto triggerReserveOptions)
        {
            services
                .AddSingleton<ITriggerRegistry, TriggerRegistry>()

                .AddScoped<ITriggerRunner, TriggerRunner<Guid>>()
                .AddSingleton(triggerServiceOptions)

                .AddScoped<ITriggerSelectQuery<Guid>, EFTriggerSelectQuery<Guid>>()
                
                .AddSingleton(triggerOptions)
                .AddScoped<IEventJsonSerializer, EventJsonSerializer<Guid>>()
                ;

            services
                .AddScoped<TriggerEventRaiser<Guid>>()
                .AddScoped<ITriggerEventRaiser<Guid>>(s => s.GetRequiredService<TriggerEventRaiser<Guid>>())
                .Decorate<ITriggerEventRaiser<Guid>, TriggerEventRaiserExceptionDbDecorator<Guid>>()
                .Decorate<ITriggerEventRaiser<Guid>, TriggerEventRaiserAfterTransactionCompleteDecorator<Guid>>()
                
                .AddSingleton(
                    new TriggerEventOutboxRunner<Guid>.OptionsDto() 
                    { 
                        NoDecoratorEventRaiserFactory = static s => s.GetRequiredService<TriggerEventRaiser<Guid>>(),                        
                    }
                )
                
                .AddScoped<TriggerEventRaiserExceptionDbDecorator<Guid>.IQuery, EFTriggerEventRaiserExceptionDbDecoratorQuery<Guid>>();

            services
                .AddScoped<ITriggerReserveProvider<Guid>, RedisTriggerReserveProvider<Guid>>()
                .AddSingleton(triggerReserveOptions)

                .AddScoped<ITriggerQueueProvider<Guid>, RedisTriggerQueueProvider<Guid>>()
                .AddSingleton(triggerQueueOptions)
                .AddSingleton<IRedisTriggerQueueNotifyState, RedisTriggerQueueNotifyState<Guid>>()
                .AddScoped<IRedisTriggerQueueNotificationRunner, RedisTriggerQueueNotificationRunner<Guid>>()

                .AddScoped<ITriggerQueueContext<Guid>, TriggerQueueContext<Guid>>()
                ;

            return services;
        }

        public static IServiceCollection AddInboxOutbox(
            this IServiceCollection services,
            InboxRunner<Guid>.OptionsDto inboxRunnerOptions,
            EFInboxConsumerService<Guid>.Options inboxConsumerOptions,
            EFInboxDbProvider<Guid>.Options inboxDbProviderOptions,
            EFOutboxDbProvider1<Guid>.Options outboxDbProviderOptions,
            InboxRegistryDto inboxRegistry,
            OutboxRegistryDto outboxRegistry) 
        {
            services

                .AddScoped<IOutboxSender<Guid>, EFOutboxSender<Guid>>()

                .AddScoped<EFInboxDbProvider<Guid>>()
                .AddSingleton(inboxDbProviderOptions)
                .AddScoped<IProcessDbProvider<Guid>>(s => s.GetRequiredService<EFInboxDbProvider<Guid>>())
                .AddScoped<EFOutboxDbProvider1<Guid>>()
                .AddSingleton(outboxDbProviderOptions)
                .AddScoped<IProcessDbProvider<Guid>>(s => s.GetRequiredService<EFOutboxDbProvider1<Guid>>())

                .AddScoped<IHeaderJsonSerializer, HeaderJsonSerializer>()

                .AddScoped<IInboxSetter, InboxSetter>()
                .AddScoped<IOutboxSetter, OutboxSetter>()

                .AddScoped<IInboxRunner, InboxRunner<Guid>>()
                .AddSingleton(inboxRunnerOptions)

                .AddScoped<IInboxConsumerService, EFInboxConsumerService<Guid>>()
                .AddSingleton(inboxConsumerOptions)

                .AddScoped<IClassifierRepository<Guid>, EFClassifierRepository<Guid>>()
                .AddSingleton<EFClassifierRepository<Guid>.CachState>()

                .AddScoped(s => new OutboxRangeProcessHandler1<Guid>(
                    s.GetRequiredService<IProcessRepository<Guid>>(),
                    s.GetRequiredService<ITriggerRepository<Guid>>(),
                    s.GetRequiredService<IProcessSetter>(),
                    s.GetRequiredService<IQueueProviderFactory>(),
                    s.GetRequiredService<IDateTimeProvider>(),
                    s.GetRequiredService<IOutboxSetter>(),
                    s.GetRequiredService<IHeaderJsonSerializer>(),
                    Presets<Guid>.Preset1
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

        public static IServiceCollection AddTestService(this IServiceCollection services)
        {
            services.AddSingleton<TestService>();

            return services;
        }

        public static IServiceCollection AddSchemaProcess(
            this IServiceCollection services,
            TokenExecutionService<Guid>.OptionsDto tokenExecutionOptions,
            TriggerStateService<Guid>.OptionsDto triggerStateOptions,
            params SchemaProcessRegistrationDto[] registrations)
        {
            services
                .AddSingleton<ISchemaRegistry, SchemaRegistry>()
                .AddScoped<ISchemaValidator, SchemaValidator<Guid>>()

                .AddScoped<ISchemaService<Guid>, SchemaService<Guid>>()
                .AddScoped<SchemaService<Guid>.IQueries, EFSchemaServiceQueries<Guid>>()

                .AddScoped<ITriggerStateService<Guid>, TriggerStateService<Guid>>()
                .AddSingleton(triggerStateOptions)
                
                .AddScoped<ITokenExecutionService<Guid>, TokenExecutionService<Guid>>()
                .AddScoped<TokenExecutionService<Guid>.IQueries, EFTokenExecutionServiceQueries<Guid>>()
                .AddSingleton(tokenExecutionOptions)

                .AddScoped<ISchemaSerializer, SchemaSerializer>()
                .AddScoped<IActionStateSerializer, ActionStateSerializer>()
                .AddScoped<SchemaSingleProcessHandler<Guid>>()

                .AddScoped<ISchemaProcessActionSetter, SchemaProcessActionSetter>()
                .AddScoped<ISchemaProcessActionSetter.ICommonSetter, SchemaProcessActionSetter.CommonSetterImpl>()
                .AddScoped<ISchemaProcessActionSetter.IServiceTaskSetter, SchemaProcessActionSetter.ServiceTaskSetterImpl>()
                .AddScoped<ISchemaProcessActionSetter.IConditionSetter, SchemaProcessActionSetter.ConditionSetterImpl>()
                .AddScoped<ISchemaProcessActionSetter.ITimerSetter, SchemaProcessActionSetter.TimerSetterImpl>()
                ;

            foreach (var elem in registrations)
            {
                services.AddSingleton(elem);

                services.AddScoped(elem.ProcessHandlerType);
                //// services.AddScoped<ISchemaProcessHandler<Guid>>(s => (ISchemaProcessHandler<Guid>)s.GetRequiredService(elem.ProcessHandlerType));

                services.TryAddScoped(elem.ProcessStateHandlerType);
                ////  services.AddScoped<ISchemaProcessStateHandler<Guid>>(s => (ISchemaProcessStateHandler<Guid>)s.GetRequiredService(elem.ProcessStateHandlerType));
            }

            return services;
        }

        public static IServiceCollection AddRedis(
            this IServiceCollection services,
            RedisConnectionFactory.OptionsDto connectionOptions)
        {
            services
                .AddSingleton(connectionOptions)
                .AddSingleton<IRedisConnectionFactory, RedisConnectionFactory>();

            return services;
        }

        public static IServiceCollection AddRedisExternalCounter(
            this IServiceCollection services,
            RedisExternalCounterProvider.OptionsDto providerOptions
            )
        {
            if (!UseInMemoryExternalCounter)
            {
                services
                    .AddSingleton(providerOptions)
                    .AddScoped<IExternalCounterProvider, RedisExternalCounterProvider>()
                    .AddScoped<IExternalCounterContext, ExternalCounterContext>();
            }
            else 
            {
                services
                    .AddSingleton<IExternalCounterProvider, InMemoryExternalCounterProvider>();
            }
            
            return services;
        }
    }
}
