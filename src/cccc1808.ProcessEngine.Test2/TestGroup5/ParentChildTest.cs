using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Stream;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Service;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Service.Serializers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Entity;
using cccc1808.ProcessEngine.Model.SimpleSchema.Implementation.Handlers;
using cccc1808.ProcessEngine.Test2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process5;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup5
{
    [Collection(FixtureCollection.Name)]
    public class ParentChildTest
        : IAsyncLifetime
    {
        private readonly FixtureCollection.Fixture _fixture;
        private readonly TestService _testService;

        public ParentChildTest(
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
                var schemaProcessStateHandler = scope.ServiceProvider.GetRequiredService<SchemaProcessStateTypelessHandler<Guid>>();

                validator.Validate(TestSchemaProcessHandler51.ProcessType, TestSchemaProcessHandler51.Schema, TestSchemaProcessHandler51.UseSignalCode);
                validator.Validate(TestSchemaProcessHandler52.ProcessType, TestSchemaProcessHandler52.Schema, TestSchemaProcessHandler52.UseSignalCode);

                dbContext.Set<SchemaDbEntity<Guid>>().Add(
                    new SchemaDbEntity<Guid>(
                        Guid.NewGuid(),
                        TestSchemaProcessHandler51.ProcessType.ProcessType,
                        TestSchemaProcessHandler51.ProcessType.ProcessVersion,
                        schemaSerializaer.Serialize(TestSchemaProcessHandler51.Schema))
                    );
                dbContext.Set<SchemaDbEntity<Guid>>().Add(
                    new SchemaDbEntity<Guid>(
                        Guid.NewGuid(),
                        TestSchemaProcessHandler52.ProcessType.ProcessType,
                        TestSchemaProcessHandler52.ProcessType.ProcessVersion,
                        schemaSerializaer.Serialize(TestSchemaProcessHandler52.Schema))
                    );

                dbContext.Set<ProcessDbEntity<Guid>>().Add(
                    new ProcessDbEntity<Guid>(
                        processId,
                        TestSchemaProcessHandler51.ProcessType.ProcessType,
                        TestSchemaProcessHandler51.ProcessType.ProcessVersion,
                        1,
                        DateTimeOffset.MinValue,
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
                        newSignalCounter: 0,
                        useSignals: true),
                    CancellationToken.None);

                dbContext.Set<SchemaProcessDataDbEntity<Guid>>().Add(
                    new SchemaProcessDataDbEntity<Guid>(
                        id: Guid.NewGuid(),
                        processId: processId,
                        rootTriggerKey: rootTriggerKey,
                        currentTokenId: TestSchemaProcessHandler51.Schema.StartTokenId
                        )
                    {
                        ProcessState = schemaProcessStateHandler.SerializeProcessState(
                            null, 
                            TestSchemaProcessHandler51.CreateProcessState(childProcessCount: 1))
                    }
                    );

                await dbContext.SaveChangesAsync(CancellationToken.None);
            }

            // 2) Родительский процесс запускает дочерние процессы.
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
                    e => e.CurrentTokenId.ShouldBe("1"));
            }

            // 3) Дочерние процессы выполняются.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunProcessRunnerAsync(scope.ServiceProvider);
            }

            // 4) Обрабатываем триггеры. Родительский процесс должен пробудится.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunTriggerConsumerRunnerAsync(scope.ServiceProvider);                
                await _testService.RunTriggerDbRunnerAsync(scope.ServiceProvider);

                await _testService.RunTriggerConsumerRunnerAsync(scope.ServiceProvider);
                await _testService.RunTriggerDbRunnerAsync(scope.ServiceProvider);

                var processes = await _testService.LoadProcessAsync(scope.ServiceProvider);
                var process = processes.Single(e => e.Id == processId);

                process.ShouldSatisfyAllConditions(
                    e => e.Status.ShouldBe(ProcessStatusEnum.AsyncExecute));
            }

            // 5) Завершаем родительский процесс.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunProcessRunnerAsync(scope.ServiceProvider);

                var processes = await _testService.LoadProcessAsync(scope.ServiceProvider);
                var process = processes.Single(e => e.Id == processId);

                process.ShouldSatisfyAllConditions(
                    e => e.Status.ShouldBe(ProcessStatusEnum.Complete));
            }
        }
    }
}
