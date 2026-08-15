using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Test2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Tests
{
    /// <summary>
    /// /// <summary>
    /// https://wiki.denhome.ru/bin/view/Проекты%20и%20репозитории/Библиотеки/Движок%20cccc1808.%20ProcessEngine/Примеры/
    /// Пример 1. Вариант 5.
    /// </summary>
    /// </summary>
    [Collection(FixtureCollection.Name)]
    public class ParentChildTest3
        : IAsyncLifetime
    {
        private readonly FixtureCollection.Fixture _fixture;
        private readonly TestService _testService;

        private static ProcessRegistryDto ParentProcessRegistry { get; }
            = new ProcessRegistryDto(
                new ProcessTypeUniqueDto(new ProcessTypeDto(3, 1), 1),
                new ProcessTypeMetadata(IsSignleExecuteProcess: true));

        private static ProcessRegistryDto ChildProcessRegistry { get; }
            = new ProcessRegistryDto(
                new ProcessTypeUniqueDto(new ProcessTypeDto(4, 1), 1),
                new ProcessTypeMetadata(IsSignleExecuteProcess: true));

        public ParentChildTest3(
            FixtureCollection.Fixture fixture)
        {
            _fixture = fixture;
            _testService = fixture.ServiceProvider.GetRequiredService<TestService>();
        }

        public async Task InitializeAsync()
        {
            await _fixture.PrepareEnvironmentAsync();
        }

        public async Task DisposeAsync()
        {
            await _fixture.CleanEnvironmentAsync();
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Fact(Timeout = FixtureCollection.TestTimeout)]
        public async Task Test1()
        {
            var idGenerator = _fixture.ServiceProvider.GetRequiredService<IIdGenerator<Guid>>();

            var processId = await idGenerator.NextAsync(default);
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
                var testState = scope.ServiceProvider.GetRequiredService<TestProcessBody.TestState>();
                var processQueueContext = scope.ServiceProvider.GetRequiredService<IProcessQueueContext<Guid>>();
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();

                await using var transaction = await transactionManager.StartTransactionAsync(CancellationToken.None);

                dbContext.Set<ProcessDbEntity<Guid>>().Add(
                    new ProcessDbEntity<Guid>(
                        processId,
                        ParentProcessRegistry.Unique,
                        false,
                        ProcessStatusEnum.AsyncExecute,
                        null
                        )
                    );

                await dbContext.SaveChangesAsync(default);
                await transaction.CommitAsync(CancellationToken.None);

                testState.StepRange = Handler;
            }

            // 1) Выполняется родительский процесс.
            // Создается триггер (если пакетно, то отдельной транзакций).
            // Создаются и запускаются дочерние процессы.
            Guid childProcessId;
            string parentTriggerKey;
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunProcessDbSelectRunnerAsync(scope.ServiceProvider);
                await _testService.RunProcessRunnerAsync(
                    scope.ServiceProvider, 
                    withProcessNotification: true);

                // assert.
                {
                    var childProcessData = await _testService.LoadAsync<ChildProcessDbEntity>(scope.ServiceProvider);
                    var allProceses = await _testService.LoadProcessAsync(scope.ServiceProvider);
                    var triggers = await _testService.LoadTriggersAsync(scope.ServiceProvider);

                    childProcessData.ShouldSatisfyAllConditions(
                        e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                            e => e.ParentProcessId.ShouldBe(processId),
                            e => e.ActiveParentProcessId.ShouldBe(processId)));

                    childProcessId = childProcessData.Single().ProcessId;
                    parentTriggerKey = childProcessData.Single().ParentTriggerKey;

                    allProceses.ShouldSatisfyAllConditions(
                        e => e.Length.ShouldBe(2),
                        e => e.ShouldContain(e => e.Id == childProcessId),
                        e => e.Single(e => e.Id == childProcessId).ShouldSatisfyAllConditions(
                            e => e.Status.ShouldBe(ProcessStatusEnum.AsyncExecute)),
                         e => e.Single(e => e.Id == processId).ShouldSatisfyAllConditions(
                            e => e.Status.ShouldBe(ProcessStatusEnum.WaitEvent))
                        );

                    triggers.ShouldSatisfyAllConditions(
                        e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                            e => e.Key.ShouldBe(parentTriggerKey),
                            e => e.SignalCounter1.ShouldBe(0),
                            e => e.StreamProcessIsWaiting.ShouldBe(false),
                            e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeFalse(),
                            e => e.IsActivated.ShouldBeFalse(),
                            e => e.IsCompleted.ShouldBeFalse()));
                }
            }

            // 2) Обработка событий триггеров.
            // StreamTrigger фиксирует, что родительский процес уснул.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunTriggerConsumerRunnerAsync(scope.ServiceProvider, withTriggerNotification: false);

                // assert.
                {
                    var triggers = await _testService.LoadTriggersAsync(scope.ServiceProvider);
                    triggers.ShouldSatisfyAllConditions(
                        e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                            e => e.Key.ShouldBe(parentTriggerKey),
                            e => e.SignalCounter1.ShouldBe(0),
                            e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeTrue(),
                            e => e.IsActivated.ShouldBeFalse(),
                            e => e.IsCompleted.ShouldBeFalse()));
                }
            }

            // 3) Выполнение дочерних процессов.
            // По завершению на родительский триггер публикуется событие.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunProcessRunnerAsync(
                    scope.ServiceProvider,
                    withProcessNotification: false);

                // assert.
                {
                    var childProcessData = await _testService.LoadAsync<ChildProcessDbEntity>(scope.ServiceProvider);
                    var allProceses = await _testService.LoadProcessAsync(scope.ServiceProvider);
                    var triggers = await _testService.LoadTriggersAsync(scope.ServiceProvider);

                    childProcessData.ShouldSatisfyAllConditions(
                        e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                            e => e.ProcessId.ShouldBe(childProcessId),
                            e => e.ParentProcessId.ShouldBe(processId),
                            e => e.ActiveParentProcessId.ShouldBeNull()));

                    allProceses.ShouldSatisfyAllConditions(
                        e => e.Length.ShouldBe(2),
                        e => e.ShouldContain(e => e.Id == childProcessId),
                        e => e.Single(e => e.Id == childProcessId).ShouldSatisfyAllConditions(
                            e => e.Status.ShouldBe(ProcessStatusEnum.Complete)),
                         e => e.Single(e => e.Id == processId).ShouldSatisfyAllConditions(
                            e => e.Status.ShouldBe(ProcessStatusEnum.WaitEvent))
                        );

                    triggers.ShouldSatisfyAllConditions(
                        e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                            e => e.Key.ShouldBe(parentTriggerKey),
                            e => e.SignalCounter1.ShouldBe(0),
                            e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeTrue(),
                            e => e.IsActivated.ShouldBeFalse(),
                            e => e.IsCompleted.ShouldBeFalse()));
                }
            }

            // 4) Обработка событий триггеров.
            // События по SimpleStreamTrigger, активация триггера.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunTriggerConsumerRunnerAsync(scope.ServiceProvider, withTriggerNotification: true);

                // assert.
                {
                    var triggers = await _testService.LoadTriggersAsync(scope.ServiceProvider);
                    triggers.ShouldSatisfyAllConditions(
                        e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                            e => e.Key.ShouldBe(parentTriggerKey),
                            e => e.SignalCounter1.ShouldBe(0),
                            e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeFalse(),
                            e => e.IsActivated.ShouldBeTrue(),
                            e => e.IsCompleted.ShouldBeFalse()));
                }
            }

            // 5) Обработка активных триггеров. SimpleStreamTrigger.
            // Пробуждение родительского процесса.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunTriggerExecuteRunnerAsync(
                    scope.ServiceProvider, 
                    withTriggerNotification: false,
                    withProcessNotification: true
                    );

                // assert.
                {
                    var allProceses = await _testService.LoadProcessAsync(scope.ServiceProvider);
                    var triggers = await _testService.LoadTriggersAsync(scope.ServiceProvider);

                    allProceses.ShouldSatisfyAllConditions(
                        e => e.Length.ShouldBe(2),
                        e => e.ShouldContain(e => e.Id == childProcessId),
                        e => e.Single(e => e.Id == childProcessId).ShouldSatisfyAllConditions(
                            e => e.Status.ShouldBe(ProcessStatusEnum.Complete)),
                         e => e.Single(e => e.Id == processId).ShouldSatisfyAllConditions(
                            e => e.Status.ShouldBe(ProcessStatusEnum.AsyncExecute))
                        );

                    triggers.ShouldBeEmpty();
                }
            }

            // 6) Выполняется родительский процесс.
            // Завершение родительского процесса.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunProcessRunnerAsync(
                    scope.ServiceProvider,
                    withProcessNotification: false);

                // assert.
                {
                    var allProceses = await _testService.LoadProcessAsync(scope.ServiceProvider);

                    allProceses.ShouldSatisfyAllConditions(
                        e => e.Length.ShouldBe(2),
                        e => e.ShouldContain(e => e.Id == childProcessId),
                        e => e.Single(e => e.Id == childProcessId).ShouldSatisfyAllConditions(
                            e => e.Status.ShouldBe(ProcessStatusEnum.Complete)),
                         e => e.Single(e => e.Id == processId).ShouldSatisfyAllConditions(
                            e => e.Status.ShouldBe(ProcessStatusEnum.Complete))
                        );
                }
            }
        }

        ///// <summary>
        ///// TODO: подвинуть в другой test class.
        ///// </summary>
        ///// <returns></returns>
        //[Fact(Timeout = FixtureCollection.TestTimeout)]
        //public async Task ReservationSubTestAsync()
        //{
        //    await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
        //    {
        //        var options = scope.ServiceProvider.GetRequiredService<RedisProcessReservationOptions>();
        //        var connectionFactory = scope.ServiceProvider.GetRequiredService<IRedisConnectionFactory>();
        //        var provider = scope.ServiceProvider.GetRequiredService<IProcessReservationProvider<Guid>>();

        //        var connection = await connectionFactory.GetAsync(options.ConnectionName, CancellationToken.None);
        //        await using var subscribe = await connection.SubscribeAsync(options.ChannelName, CancellationToken.None);
        //        await using var subscribeEnumerator = subscribe.ChannelMessages.GetAsyncEnumerator();

        //        // 1)
        //        await provider.TryReserveAsync([Guid.Empty], DateTimeOffset.Now.AddHours(1), CancellationToken.None);

        //        (await subscribeEnumerator.MoveNextAsync()).ShouldBeTrue();
        //        JsonDocument.Parse((string)subscribeEnumerator.Current.Message)
        //            .Deserialize<ReservationMessageDto<Guid>>()
        //            .ShouldSatisfyAllConditions(
        //                e => e.ProcessId.ShouldBe(Guid.Empty),
        //                e => e.IsReserveOrUnreserve.ShouldBeTrue()
        //                );

        //        // 2)
        //        await provider.UnreserveAsync([Guid.Empty], CancellationToken.None);

        //        (await subscribeEnumerator.MoveNextAsync()).ShouldBeTrue();
        //        JsonDocument.Parse((string)subscribeEnumerator.Current.Message)
        //            .Deserialize<ReservationMessageDto<Guid>>()
        //            .ShouldSatisfyAllConditions(
        //                e => e.ProcessId.ShouldBe(Guid.Empty),
        //                e => e.IsReserveOrUnreserve.ShouldBeFalse()
        //                );
        //    }
        //}

        //[Fact(Timeout = FixtureCollection.TestTimeout)]
        //public async Task RedisQueueTestAsync()
        //{
        //    await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
        //    {
        //        var message = new IRedisReservationQueue<Guid>.MessageDto(
        //            new ProcessRegistryDto(new ProcessTypeDto(1, 1), 1),
        //            Guid.Empty);

        //        var state = scope.ServiceProvider.GetRequiredService<IRedisNotifyQueueState>();
        //        var queueProvider = scope.ServiceProvider.GetRequiredService<IRedisReservationQueue<Guid>>();
        //        var runner = scope.ServiceProvider.GetRequiredService<IRedisProcessQueueNotificationRunner>();

        //        {
        //            var consumeResult = await queueProvider.ConsumeAsync(5, batchTimeout: TimeSpan.FromSeconds(2), default);
        //            consumeResult.ShouldBeEmpty();

        //            state.GetQueueWithMessages().ShouldBeEmpty();
        //            var waitTask = await state.AllQueueEmptySleepAsync(default);
        //            waitTask.IsCompleted.ShouldBeFalse();
        //        }

        //        {
        //            var runnerTask = runner.RunAsync(one: true, default);

        //            {
        //                var produceResult = await queueProvider.ProduceAsync([message], default);
        //                produceResult.ShouldBeEmpty();
        //            }

        //            {
        //                await runnerTask;

        //                state.GetQueueWithMessages().ShouldNotBeEmpty();
        //                var waitTask = await state.AllQueueEmptySleepAsync(default);
        //                waitTask.IsCompleted.ShouldBeTrue();
        //            }
        //        }

        //        {
        //            var consumeResult = await queueProvider.ConsumeAsync(5, batchTimeout: TimeSpan.FromSeconds(2), default);
        //            consumeResult.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
        //                e => e.Registry.ProcessType.ShouldBe(message.Registry.ProcessType),
        //                e => e.Registry.ProcessType.ProcessVersion.ShouldBe(message.Registry.ProcessType.ProcessVersion),
        //                e => e.ProcessId.ShouldBe(message.ProcessId));

        //            state.GetQueueWithMessages().ShouldBeEmpty();
        //            var waitTask = await state.AllQueueEmptySleepAsync(default);
        //            waitTask.IsCompleted.ShouldBeFalse();
        //        }
        //    }
        //}

        private static async ValueTask Handler(
            IServiceProvider serviceProvider,
            ExecuteStepByStepGroupMiddleware<Guid>.ExecuteGroup group)
        {
            var process = group.Group.Values.First();

            switch (process.Process.Info.Registry.Unique.ProcessType.ProcessType)
            {
                case 3:
                    {
                        var idGenerator = serviceProvider.GetRequiredService<IIdGenerator<Guid>>();
                        var triggerRepository = serviceProvider.GetRequiredService<ITriggerRepository<Guid>>();
                        var triggerOptions = serviceProvider.GetRequiredService<TriggerRunner<Guid>.OptionsDto>();
                        var dbcontext = serviceProvider.GetRequiredService<IEFDbContext>();
                        var setter = serviceProvider.GetRequiredService<IProcessSetter>();
                        var externalCounterContext = serviceProvider.GetRequiredService<IExternalCounterContext>();
                        var processQueueContext = serviceProvider.GetRequiredService<IProcessQueueContext<Guid>>();

                        var childProcessesCreated = await dbcontext
                            .Set<ChildProcessDbEntity>()
                            .Where(e => e.ParentProcessId == process.Id)
                            .AnyAsync();

                        if (!childProcessesCreated)
                        {
                            var childCount = 1;
                            var triggerKey = Guid.NewGuid().ToString();

                            await triggerRepository.CreateTriggerAsync(
                                ITriggerRepository<Guid>.CreateTriggerDto.SimpleStreamTrigger(
                                    triggerKey,
                                    DateTimeOffset.MinValue,
                                    process.Id,
                                    isRangeTrigger: true,
                                    ParentProcessTriggerHandler.Name,
                                    1,
                                    false,
                                    streamProcessIsWaiting: false,
                                    newSignalCounter: 0,
                                    isChildTrigger: false), 
                                CancellationToken.None);

                            await externalCounterContext.CreateCounterAsync(triggerKey, childCount, default);

                            processQueueContext.IncreseBufferCapacity(childCount);
                            for (int i = 0; i < childCount; i++)
                            {
                                var processId = await idGenerator.NextAsync(default);
                                dbcontext.Set<ProcessDbEntity<Guid>>().Add(
                                    new ProcessDbEntity<Guid>(
                                        processId,
                                        ChildProcessRegistry.Unique,
                                        false,
                                        ProcessStatusEnum.AsyncExecute,
                                        null
                                        ));
                                dbcontext.Set<ChildProcessDbEntity>().Add(
                                    new ChildProcessDbEntity(
                                        processId,
                                        process.Id,
                                        process.Id,
                                        triggerKey));

                                processQueueContext.ProcessToExecute(
                                    IProcessQueueContext<Guid>.ProcessDto.ProcessToExecute(
                                        processId,
                                        ChildProcessRegistry
                                        )
                                    );
                            }

                            setter.SetStatus(
                                process,
                                ProcessStatusEnum.WaitEvent);

                            process.AddComponent<IStreamTriggerComponent>(
                                new StreamTriggerComponent(
                                    triggerOptions.Consumer_TriggerEventQueues.Single().QueueName,
                                    [triggerKey])
                                );
                        }
                        else
                        {
                            setter.SetStatus(
                                process,
                                ProcessStatusEnum.Complete);

                            // TODO: можно в самом триггере или тут.
                            // await externalCounterProvider.RemoveCounterAsync(triggerKey, default);
                        }

                        break;
                    }

                case 4:
                    {
                        var setter = serviceProvider.GetRequiredService<IProcessSetter>();
                        var triggerOptions = serviceProvider.GetRequiredService<TriggerRunner<Guid>.OptionsDto>();
                        var triggerEventRaiser = serviceProvider.GetRequiredService<ITriggerEventRaiser<Guid>>();
                        var externalCounterProvider = serviceProvider.GetRequiredService<IExternalCounterProvider>();
                        var externalCounterContext = serviceProvider.GetRequiredService<IExternalCounterContext>();

                        var processIdString = process.Id.ToString();
                        var component = process.GetComponent<ChildProcessDbEntity>();

                        var counterCorrupted = !await externalCounterProvider.CounterExists(component.ParentTriggerKey, default);

                        if (!counterCorrupted)
                        {
                            // Счетчик существует.

                            // 1) Если текущий процес уже менял счетчик, то сбрасываем это.
                            await externalCounterProvider.CompensateCounterAsync(component.ParentTriggerKey, processIdString);

                            // 2) Меняем значение счетчика и фиксируем процесс.
                            var counter = await externalCounterContext.TryDecrementCounterAsync(component.ParentTriggerKey, processIdString);

                             if (counter == 0)
                            {
                                // Если счетчик исчерпан, то шлем событие на триггер.
                                await triggerEventRaiser.RaiseAsync(
                                    [new ITriggerEventRaiser<Guid>.RaiseContainer(
                                    triggerOptions.Consumer_TriggerEventQueues.Single().QueueName,
                                    component.ParentProcessId,
                                    new SignalSimpleStreamTriggerEvent(component.ParentTriggerKey)
                                    )],
                                    default);
                            }
                            else if (counter < 0)
                            {
                                counterCorrupted = true;
                            }
                        }

                        if (counterCorrupted)
                        {
                            // Счетчик был потерян (падение хранилища) или поврежден.

                            // Тогда просто публикуем событие на триггер и timeout т.к. мы не можем использовать счетчик
                            // и теперь триггер обязан каждый раз првоерять условие.
                            await triggerEventRaiser.RaiseAsync(
                                [new ITriggerEventRaiser<Guid>.RaiseContainer(
                                    // Для этой ветки можно использовать очередь с большей задержкой (окном аггрегации).
                                    triggerOptions.Consumer_TriggerEventQueues.Single().QueueName,
                                    component.ParentProcessId,
                                    new SignalSimpleStreamTriggerEvent(component.ParentTriggerKey)
                                    )],
                                default);
                            
                            // TODO можно добавить timeout тут, если значение меньше указанного. Но также timeout в хендлере триггера после активации.
                        }

                        setter.SetStatus(
                            process,
                            ProcessStatusEnum.Complete);

                        // Убираем блокирующий ключ, чтобы условие выполнялось.
                        component.ActiveParentProcessId = null;

                        break;
                    }

                default: throw new NotImplementedException();
            }
        }
    }
}
