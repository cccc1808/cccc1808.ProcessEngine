using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Stream;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Service;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Service.Serializers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Entity;
using cccc1808.ProcessEngine.Test2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process1;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process2;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process4;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup5
{
    [Collection(FixtureCollection.Name)]
    public class SimpleSchemaProcessTest
        : IAsyncLifetime
    {
        private readonly FixtureCollection.Fixture _fixture;
        private readonly TestService _testService;

        public SimpleSchemaProcessTest(
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
            var processId = Guid.NewGuid();
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var schemaSerializaer = scope.ServiceProvider.GetRequiredService<ISchemaSerializer>();
                var validator = scope.ServiceProvider.GetRequiredService<ISchemaValidator>();
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var triggerRepository = scope.ServiceProvider.GetRequiredService<ITriggerRepository<Guid>>();

                validator.Validate(TestSchemaProcessHandler.ProcessType, TestSchemaProcessHandler.Schema);

                dbContext.Set<SchemaDbEntity<Guid>>().Add(
                    new SchemaDbEntity<Guid>(
                        Guid.NewGuid(),
                        TestSchemaProcessHandler.ProcessType.ProcessType,
                        TestSchemaProcessHandler.ProcessType.ProcessVersion,
                        schemaSerializaer.Serialize(TestSchemaProcessHandler.Schema))
                    );

                dbContext.Set<ProcessDbEntity<Guid>>().Add(
                    new ProcessDbEntity<Guid>(
                        processId,
                        TestSchemaProcessHandler.ProcessType.ProcessType,
                        TestSchemaProcessHandler.ProcessType.ProcessVersion,
                        1,
                        false,
                        ProcessStatusEnum.AsyncExecute,
                        null
                        )
                    );

                var rootTriggerKey = Guid.NewGuid().ToString();
                await triggerRepository.CreateTriggerAsync(
                    ITriggerRepository<Guid>.CreateTriggerDto.SimpleRootStreamTrigger(
                        key: rootTriggerKey,
                        timerDate: DateTimeOffset.MinValue,
                        processId: processId,
                        isRangeTrigger: true,
                        handlerKey: NoWakeupStreamTriggerRangeHandler<Guid>.Name,
                        priority: 1,
                        isActivated: false,
                        streamProcessIsWaiting: false,
                        newSignalCounter: 0), 
                    CancellationToken.None);

                dbContext.Set<SchemaProcessDataDbEntity<Guid>>().Add(
                    new SchemaProcessDataDbEntity<Guid>(
                        id: Guid.NewGuid(),
                        processId: processId,
                        rootTriggerKey: rootTriggerKey,
                        currentTokenId: TestSchemaProcessHandler.Schema.StartTokenId
                        )
                    );

                await dbContext.SaveChangesAsync(CancellationToken.None);
            }

            // 2) Выполняем 1 токен схемы, переходим на 2 токен.
            // на 2 токене создаем таймер и засыпаем.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunProcessRunnerAsync(scope.ServiceProvider);

                var processes = await _testService.LoadProcessAsync(scope.ServiceProvider);
                var processDatas = await _testService.LoadAsync<SchemaProcessDataDbEntity<Guid>>(scope.ServiceProvider);

                var process = processes.Single(e => e.Id == processId);
                var processData = processDatas.Single(e => e.ProcessId == processId);

                process.ShouldSatisfyAllConditions(
                    e => e.Status.ShouldBe(ProcessStatusEnum.WaitEvent));

                processData.ShouldSatisfyAllConditions(
                    e => e.CurrentTokenId.ShouldBe("2"));
            }

            // 3)
            // .1) Выполняем дочерний таймер триггер.
            // .2) Выполняем корневой триггер (событие и засыпании и событие об актвации от дочернего).
            // .3) Выполняем 2 токен схемы, процесс завершается.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunTriggerDbSelectRunnerAsync(scope.ServiceProvider, withNotification: true);
                await _testService.RunTriggerExecuteRunnerAsync(scope.ServiceProvider, withTriggerNotification: false);

                await _testService.RunTriggerConsumerRunnerAsync(scope.ServiceProvider, withNotification: true);
                await _testService.RunTriggerExecuteRunnerAsync(scope.ServiceProvider, withTriggerNotification: false);

                await _testService.RunProcessRunnerAsync(scope.ServiceProvider);

                var processes = await _testService.LoadProcessAsync(scope.ServiceProvider);
                var processDatas = await _testService.LoadAsync<SchemaProcessDataDbEntity<Guid>>(scope.ServiceProvider);

                var process = processes.Single(e => e.Id == processId);
                var processData = processDatas.Single(e => e.ProcessId == processId);

                process.ShouldSatisfyAllConditions(
                    e => e.Status.ShouldBe(ProcessStatusEnum.Complete));

                processData.ShouldSatisfyAllConditions(
                    e => e.CurrentTokenId.ShouldBe("2"));
            }
        }

        [Fact(Timeout = FixtureCollection.TestTimeout)]
        public async Task Test2()
        {
            var processId = Guid.NewGuid();
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var schemaSerializaer = scope.ServiceProvider.GetRequiredService<ISchemaSerializer>();
                var validator = scope.ServiceProvider.GetRequiredService<ISchemaValidator>();
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var triggerRepository = scope.ServiceProvider.GetRequiredService<ITriggerRepository<Guid>>();

                validator.Validate(TestSchemaProcessHandler2.ProcessType, TestSchemaProcessHandler2.Schema);

                dbContext.Set<SchemaDbEntity<Guid>>().Add(
                    new SchemaDbEntity<Guid>(
                        Guid.NewGuid(),
                        TestSchemaProcessHandler2.ProcessType.ProcessType,
                        TestSchemaProcessHandler2.ProcessType.ProcessVersion,
                        schemaSerializaer.Serialize(TestSchemaProcessHandler2.Schema))
                    );

                dbContext.Set<ProcessDbEntity<Guid>>().Add(
                    new ProcessDbEntity<Guid>(
                        processId,
                        TestSchemaProcessHandler2.ProcessType.ProcessType,
                        TestSchemaProcessHandler2.ProcessType.ProcessVersion,
                        1,
                        false,
                        ProcessStatusEnum.AsyncExecute,
                        null
                        )
                    );

                var rootTriggerKey = Guid.NewGuid().ToString();
                await triggerRepository.CreateTriggerAsync(
                    ITriggerRepository<Guid>.CreateTriggerDto.SimpleRootStreamTrigger(
                        key: rootTriggerKey,
                        timerDate: DateTimeOffset.MinValue,
                        processId: processId,
                        isRangeTrigger: true,
                        handlerKey: NoWakeupStreamTriggerRangeHandler<Guid>.Name,
                        priority: 1,
                        isActivated: false,
                        streamProcessIsWaiting: false,
                        newSignalCounter: 0),
                    CancellationToken.None);

                dbContext.Set<SchemaProcessDataDbEntity<Guid>>().Add(
                    new SchemaProcessDataDbEntity<Guid>(
                        id: Guid.NewGuid(),
                        processId: processId,
                        rootTriggerKey: rootTriggerKey,
                        currentTokenId: TestSchemaProcessHandler2.Schema.StartTokenId
                        )
                    );

                await dbContext.SaveChangesAsync(CancellationToken.None);
            }

            // 2) Выполняем токен 1, посылаем запрос и ждем ответ.
            string requestId;
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var schemaService = scope.ServiceProvider.GetRequiredService<ISchemaService<Guid>>();
                var stateHandler = schemaService.GetProcessStateHandler(TestSchemaProcessHandler2.ProcessType);

                await _testService.RunProcessRunnerAsync(scope.ServiceProvider);

                var processes = await _testService.LoadProcessAsync(scope.ServiceProvider);
                var processDatas = await _testService.LoadAsync<SchemaProcessDataDbEntity<Guid>>(scope.ServiceProvider);

                var process = processes.Single(e => e.Id == processId);
                var processData = processDatas.Single(e => e.ProcessId == processId);
                var tokenState = stateHandler.DeserializeTokenState(
                    processData.CurrentTokenId,
                    processData.CurrentTokenState.ShouldNotBeNull()
                    )
                    .ShouldNotBeNull()
                    .ShouldBeOfType<TestSchemaProcessHandler2.RpcTokenState>();

                process.ShouldSatisfyAllConditions(
                    e => e.Status.ShouldBe(ProcessStatusEnum.WaitEvent));

                processData.ShouldSatisfyAllConditions(
                    e => e.CurrentTokenId.ShouldBe("1"));

                tokenState.ShouldSatisfyAllConditions(
                    e => e.CorrelationId.ShouldNotBeNull());

                requestId = tokenState.CorrelationId;
            }

            // 3) 
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var externalHandler = scope.ServiceProvider.GetRequiredService<ExternalHandlers2>();

                var process = await _testService.LoadProcessContainerAsync(scope.ServiceProvider, processId);
                var processDatas = process.GetComponent<ISchemaProcessComponent>();

                // 1) Пришел ответ.
                await externalHandler.ReponseReceivedAsync(
                    process, 
                    CancellationToken.None);
                
                process.ShouldSatisfyAllConditions(
                    e => e.Process.Status.ShouldBe(ProcessStatusEnum.Complete));
            }
        }

        [Fact(Timeout = FixtureCollection.TestTimeout)]
        public async Task Test4()
        {
            var processId = Guid.NewGuid();
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var schemaSerializaer = scope.ServiceProvider.GetRequiredService<ISchemaSerializer>();
                var validator = scope.ServiceProvider.GetRequiredService<ISchemaValidator>();
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var triggerRepository = scope.ServiceProvider.GetRequiredService<ITriggerRepository<Guid>>();

                validator.Validate(TestSchemaProcessHandler4.ProcessType, TestSchemaProcessHandler4.Schema);

                dbContext.Set<SchemaDbEntity<Guid>>().Add(
                    new SchemaDbEntity<Guid>(
                        Guid.NewGuid(),
                        TestSchemaProcessHandler4.ProcessType.ProcessType,
                        TestSchemaProcessHandler4.ProcessType.ProcessVersion,
                        schemaSerializaer.Serialize(TestSchemaProcessHandler4.Schema))
                    );

                dbContext.Set<ProcessDbEntity<Guid>>().Add(
                    new ProcessDbEntity<Guid>(
                        processId,
                        TestSchemaProcessHandler4.ProcessType.ProcessType,
                        TestSchemaProcessHandler4.ProcessType.ProcessVersion,
                        1,
                        false,
                        ProcessStatusEnum.WaitEvent,
                        null
                        )
                    );

                var rootTriggerKey = Guid.NewGuid().ToString();
                await triggerRepository.CreateTriggerAsync(
                    ITriggerRepository<Guid>.CreateTriggerDto.SimpleRootStreamTrigger(
                        key: rootTriggerKey,
                        timerDate: DateTimeOffset.MinValue,
                        processId: processId,
                        isRangeTrigger: true,
                        handlerKey: NoWakeupStreamTriggerRangeHandler<Guid>.Name,
                        priority: 1,
                        isActivated: false,
                        streamProcessIsWaiting: true,
                        newSignalCounter: 0),
                    CancellationToken.None);

                dbContext.Set<SchemaProcessDataDbEntity<Guid>>().Add(
                    new SchemaProcessDataDbEntity<Guid>(
                        id: Guid.NewGuid(),
                        processId: processId,
                        rootTriggerKey: rootTriggerKey,
                        currentTokenId: TestSchemaProcessHandler4.Schema.StartTokenId
                        )
                    );

                await dbContext.SaveChangesAsync(CancellationToken.None);
            }

            // 2) 
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var externalHandlerService = scope.ServiceProvider.GetRequiredService<ExternalHandlers4>();

                var process = await _testService.LoadProcessContainerAsync(scope.ServiceProvider, processId);                

                {
                    await externalHandlerService.UIUserInput1Async(
                        process,
                        inputValue: 1,
                        CancellationToken.None);

                    process.ShouldSatisfyAllConditions(
                        e => e.Process.Status.ShouldBe(ProcessStatusEnum.WaitEvent));
                }

                {
                    await externalHandlerService.UIUserInput2Async(
                        process,
                        inputValue: 1,
                        CancellationToken.None);

                    process.ShouldSatisfyAllConditions(
                        e => e.Process.Status.ShouldBe(ProcessStatusEnum.WaitEvent));
                }

                {
                    await externalHandlerService.UIUserInput3Async(
                        process,
                        inputValue: 1,
                        CancellationToken.None);

                    var state = TestSchemaProcessHandler4.GetOrCreateUserInputTokenState(
                        process.GetComponent<ISchemaProcessComponent>());

                    state.CalculatedResult.ShouldNotBeNull()
                        .ShouldBeEquivalentTo(state.UserInput1 + state.UserInput2 + state.UserInput3);

                    process.ShouldSatisfyAllConditions(
                        e => e.Process.Status.ShouldBe(ProcessStatusEnum.WaitEvent));
                }
            }
        }
    }
}
