using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Stream;
using cccc1808.ProcessEngine.Test2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services.RootTrigger;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Tests
{
    [Collection(FixtureCollection.Name)]
    public class RootTriggerTest
        : IAsyncLifetime
    {
        private readonly FixtureCollection.Fixture _fixture;
        private readonly TestService _testService;

        public RootTriggerTest(
            FixtureCollection.Fixture fixture)
        {
            _fixture = fixture;
            _testService = fixture.ServiceProvider.GetRequiredService<TestService>();
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            await _fixture.CleanEnvironmentAsync();
        }

        [Fact(Timeout = FixtureCollection.TestTimeout)]
        public async Task Test1()
        {
            var idGenerator = _fixture.ServiceProvider.GetRequiredService<IIdGenerator<Guid>>();

            var processId = await idGenerator.NextAsync(default);

            // 1) Создаем процесс.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var testState = scope.ServiceProvider.GetRequiredService<TestProcessBody.TestState>();
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();

                dbContext.Set<ProcessDbEntity<Guid>>().Add(
                    new ProcessDbEntity<Guid>(
                        processId,
                        5,
                        1,
                        1,
                        false,
                        ProcessStatusEnum.AsyncExecute,
                        null
                        )
                    );
                dbContext.Set<RootTriggerDbEntity>().Add(
                    new RootTriggerDbEntity(
                        await idGenerator.NextAsync(CancellationToken.None),
                        processId
                        )
                    );

                await dbContext.SaveChangesAsync(default);

                testState.StepRange = Handler;
            }

            // 2) Запускаем процесс 1 раз.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunProcessRunnerAsync(scope.ServiceProvider);

                // assert.
                {
                    var allProceses = await _testService.LoadProcessAsync(scope.ServiceProvider);
                    var allProcessData = await _testService.LoadAsync<RootTriggerDbEntity>(scope.ServiceProvider);
                    var triggers = await _testService.LoadTriggersAsync(scope.ServiceProvider);

                    var process = allProceses.Single();
                    var procesData = allProcessData.Single();

                    process.ShouldSatisfyAllConditions(
                        e => e.Status.ShouldBe(ProcessStatusEnum.WaitEvent));

                    procesData.ShouldSatisfyAllConditions(
                        e => e.IsFirst.ShouldBeFalse(),
                        e => e.RootTriggerId.ShouldNotBeNull(),
                        e => e.ChildTriggerId.ShouldNotBeNull());

                    var rootTrigger = triggers.First(e => e.Key == procesData.RootTriggerId.ToString());
                    var childTrigger = triggers.First(e => e.Key == procesData.ChildTriggerId.ToString());

                    rootTrigger.ShouldSatisfyAllConditions(
                        e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeFalse(),
                        e => e.SignalCounter1.ShouldNotBeNull().ShouldBe(0),
                        e => e.IsActivated.ShouldBeFalse(),
                        e => e.ChildTrigger_CompleteAfterDelivery.ShouldBeNull(),
                        e => e.ChildTrigger_RemoveAftrerDelivery.ShouldBeNull(),
                        e => e.ChildTrigger_WaitDeliveryTimestamp.ShouldBeNull()
                        );

                    childTrigger.ShouldSatisfyAllConditions(
                        e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeFalse(),
                        e => e.SignalCounter1.ShouldNotBeNull().ShouldBe(0),
                        e => e.IsActivated.ShouldBeFalse(),
                        e => e.ChildTrigger_CompleteAfterDelivery.ShouldNotBeNull().ShouldBeFalse(),
                        e => e.ChildTrigger_RemoveAftrerDelivery.ShouldNotBeNull().ShouldBeFalse(),
                        e => e.ChildTrigger_WaitDeliveryTimestamp.ShouldBeNull()
                        );
                }
            }

            // 3) Запускаем trigger runner.
            // Триггеры получают оповещение об остановке процесса.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunTriggerConsumerRunnerAsync(scope.ServiceProvider, withNotification: false);

                // assert.
                {
                    var allProceses = await _testService.LoadProcessAsync(scope.ServiceProvider);
                    var allProcessData = await _testService.LoadAsync<RootTriggerDbEntity>(scope.ServiceProvider);
                    var triggers = await _testService.LoadTriggersAsync(scope.ServiceProvider);

                    var procesData = allProcessData.Single();
                    var rootTrigger = triggers.First(e => e.Key == procesData.RootTriggerId.ToString());
                    var childTrigger = triggers.First(e => e.Key == procesData.ChildTriggerId.ToString());

                    rootTrigger.ShouldSatisfyAllConditions(
                        e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeTrue(), //
                        e => e.SignalCounter1.ShouldNotBeNull().ShouldBe(0),
                        e => e.IsActivated.ShouldBeFalse()
                        );
                    childTrigger.ShouldSatisfyAllConditions(
                        e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeTrue(), //
                        e => e.SignalCounter1.ShouldNotBeNull().ShouldBe(0),
                        e => e.IsActivated.ShouldBeFalse()
                        );
                }
            }

            // 4) Посылаем сигнал на дочерний триггер.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                // 1) Посылаем сигнал на дочерний триггер.
                {
                    var allProcessData = await _testService.LoadAsync<RootTriggerDbEntity>(scope.ServiceProvider);

                    var procesData = allProcessData.Single();

                    await _testService.SendTriggerEventAsync(
                        scope.ServiceProvider,
                        [new SignalSimpleStreamTriggerEvent(procesData.ChildTriggerId.Value.ToString())],
                        processId);
                }

                // 2) Считываем сигнал дочерним триггером.
                await _testService.RunTriggerConsumerRunnerAsync(scope.ServiceProvider, withNotification: true);

                // assert.
                {
                    var allProcessData = await _testService.LoadAsync<RootTriggerDbEntity>(scope.ServiceProvider);                                    
                    var triggers = await _testService.LoadTriggersAsync(scope.ServiceProvider);

                    var procesData = allProcessData.Single();
                    var rootTrigger = triggers.First(e => e.Key == procesData.RootTriggerId.ToString());
                    var childTrigger = triggers.First(e => e.Key == procesData.ChildTriggerId.ToString());

                    rootTrigger.ShouldSatisfyAllConditions(
                        e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeTrue(),
                        e => e.SignalCounter1.ShouldNotBeNull().ShouldBe(0),
                        e => e.IsActivated.ShouldBeFalse()
                        );
                    childTrigger.ShouldSatisfyAllConditions(
                        e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeFalse(),
                        e => e.SignalCounter1.ShouldNotBeNull().ShouldBe(0), 
                        e => e.IsActivated.ShouldBeTrue() // Активировался.
                        );
                }
            }

            // 5.1) Дочерний триггер передает сигнал на родительский триггер.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunTriggerExecuteRunnerAsync(scope.ServiceProvider, withTriggerNotification: false);
                await _testService.RunTriggerConsumerRunnerAsync(scope.ServiceProvider, withNotification: true);

                // assert.
                {
                    var allProcessData = await _testService.LoadAsync<RootTriggerDbEntity>(scope.ServiceProvider);
                    var triggers = await _testService.LoadTriggersAsync(scope.ServiceProvider);

                    var procesData = allProcessData.Single();
                    var rootTrigger = triggers.First(e => e.Key == procesData.RootTriggerId.ToString());
                    var childTrigger = triggers.First(e => e.Key == procesData.ChildTriggerId.ToString());

                    rootTrigger.ShouldSatisfyAllConditions(
                        e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeFalse(),
                        e => e.SignalCounter1.ShouldNotBeNull().ShouldBe(0),
                        e => e.IsActivated.ShouldBeTrue() // Активировался
                        );
                    childTrigger.ShouldSatisfyAllConditions(
                        e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeFalse(),
                        e => e.SignalCounter1.ShouldNotBeNull().ShouldBe(0),
                        e => e.IsActivated.ShouldBeFalse(),
                        e => e.ChildTrigger_WaitDeliveryTimestamp.ShouldNotBeNull() // Ожидает подтверждения.
                        );
                }
            }

            // 5.2) Дочерний триггер получает подтверждение получения сигнала от корневого.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunTriggerConsumerRunnerAsync(scope.ServiceProvider, withNotification: false);

                var allProcessData = await _testService.LoadAsync<RootTriggerDbEntity>(scope.ServiceProvider);
                var triggers = await _testService.LoadTriggersAsync(scope.ServiceProvider); ;

                var procesData = allProcessData.Single();
                var rootTrigger = triggers.First(e => e.Key == procesData.RootTriggerId.ToString());
                var childTrigger = triggers.First(e => e.Key == procesData.ChildTriggerId.ToString());
                childTrigger.ShouldSatisfyAllConditions(
                    e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeFalse(),
                    e => e.SignalCounter1.ShouldNotBeNull().ShouldBe(0),
                    e => e.IsActivated.ShouldBeFalse(),
                    e => e.ChildTrigger_WaitDeliveryTimestamp.ShouldBeNull() // Получил подтверждения.
                    );
            }

            // 6) root триггер запускает процесс.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunTriggerExecuteRunnerAsync(scope.ServiceProvider, withTriggerNotification: false);

                // assert.
                {
                    var allProceses = await _testService.LoadProcessAsync(scope.ServiceProvider);
                    var allProcessData = await _testService.LoadAsync<RootTriggerDbEntity>(scope.ServiceProvider);                                    
                    var triggers = await _testService.LoadTriggersAsync(scope.ServiceProvider);

                    var process = allProceses.Single();
                    var procesData = allProcessData.Single();
                    var rootTrigger = triggers.First(e => e.Key == procesData.RootTriggerId.ToString());
                    var childTrigger = triggers.First(e => e.Key == procesData.ChildTriggerId.ToString());

                    process.ShouldSatisfyAllConditions(
                        e => e.Status.ShouldBe(ProcessStatusEnum.AsyncExecute));

                    rootTrigger.ShouldSatisfyAllConditions(
                        e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeFalse(),
                        e => e.SignalCounter1.ShouldNotBeNull().ShouldBe(0),
                        e => e.IsActivated.ShouldBeFalse()
                        );
                    childTrigger.ShouldSatisfyAllConditions(
                        e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeFalse(),
                        e => e.SignalCounter1.ShouldNotBeNull().ShouldBe(0),
                        e => e.IsActivated.ShouldBeFalse()
                        );
                }
            }

            // 7) Процесс завершается.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunProcessRunnerAsync(scope.ServiceProvider);

                // assert.
                {
                    var allProceses = await _testService.LoadProcessAsync(scope.ServiceProvider);

                    var process = allProceses.Single();

                    process.ShouldSatisfyAllConditions(
                        e => e.Status.ShouldBe(ProcessStatusEnum.Complete));
                }
            }
        }

        private async ValueTask Handler(
            IServiceProvider serviceProvider,
            ExecuteStepByStepGroupMiddleware<Guid>.ExecuteGroup group)
        {
            var process = group.Group.Values.Single();
            var processData = process.GetComponent<RootTriggerDbEntity>();

            if (processData.IsFirst)
            {
                var setter = serviceProvider.GetRequiredService<IProcessSetter>();
                var triggerRepository = serviceProvider.GetRequiredService<ITriggerRepository<Guid>>();

                processData.RootTriggerId = Guid.NewGuid();
                processData.ChildTriggerId = Guid.NewGuid();

                var streamComponent = (StreamTriggerComponent)process.GetComponent<IStreamTriggerComponent>();                

                await triggerRepository.CreateTriggerRangeAsync(
                    [
                    ITriggerRepository<Guid>.CreateTriggerDto.SimpleRootStreamTrigger(
                        key: processData.RootTriggerId.ToString(),
                        timerDate: DateTimeOffset.MinValue,
                        processId: process.Id,
                        isRangeTrigger: true,
                        handlerKey: NoWakeupStreamTriggerRangeHandler<Guid>.Name,
                        priority: 0,
                        isActivated: false,
                        streamProcessIsWaiting: false,
                        newSignalCounter: 0),
                    ITriggerRepository<Guid>.CreateTriggerDto.SimpleStreamTrigger(
                        key: processData.ChildTriggerId.ToString(),
                        timerDate: DateTimeOffset.MinValue,
                        processId: process.Id,
                        isRangeTrigger: true,
                        handlerKey: ChildTriggerHandler.Name,
                        priority: 0,
                        isActivated: false,
                        streamProcessIsWaiting: false,
                        newSignalCounter: 0,
                        isChildTrigger: true),
                    ],
                    CancellationToken.None);

                streamComponent.TriggersKeys = [
                    processData.RootTriggerId.ToString(), 
                    processData.ChildTriggerId.ToString()
                    ];

                processData.IsFirst = false;
                setter.SetStatus(process, ProcessStatusEnum.WaitEvent);
            }
            else
            {
                var setter = serviceProvider.GetRequiredService<IProcessSetter>();

                setter.SetStatus(process, ProcessStatusEnum.Complete);
            }
        }
    }
}
