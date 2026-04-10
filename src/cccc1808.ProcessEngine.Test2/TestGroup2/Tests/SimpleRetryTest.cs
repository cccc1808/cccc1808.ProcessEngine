using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Tests
{
    [Collection(FixtureCollection.Name)]
    public class SimpleRetryTest
        : IAsyncLifetime
    {
        private readonly FixtureCollection.Fixture _fixture;

        public SimpleRetryTest(
            FixtureCollection.Fixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            await _fixture.CleanEnvironmentAsync();
        }

        [Fact(Timeout = FixtureCollection.TestTimeout)]
        public async Task RetryLimit()
        {
            var idGenerator = _fixture.ServiceProvider.GetRequiredService<IIdGenerator<Guid>>();
            var processId = await idGenerator.NextAsync(default);

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var testState = scope.ServiceProvider.GetRequiredService<Process1Body.TestState>();
                var runner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await runner.BuildHandler();

                dbContext.Set<ProcessDbEntity<Guid>>().Add(
                    new ProcessDbEntity<Guid>(
                        processId,
                        2,
                        1,
                        1,
                        DateTimeOffset.MinValue,
                        false,
                        ProcessStatusEnum.AsyncExecute,
                        null
                        )
                    );
                //dbContext.Set<ProcessWakeUpDbEntity<Guid>>().Add(
                //    new ProcessWakeUpDbEntity<Guid>());
                await dbContext.SaveChangesAsync(default);

                testState.StepRange = Handler;
            }

            //// 1)
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var runner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await runner.RunAsync(oneCycle: true, default);
                await runner.WaitRunningTasksAsync(default);

                var processes = await dbContext.Set<ProcessDbEntity<Guid>>()
                    .AsNoTracking()
                    .ToArrayAsync();
                var triggers = await dbContext.Set<TriggerDbEntity<Guid>>()
                    .AsNoTracking()
                    .ToArrayAsync();

                processes.ShouldSatisfyAllConditions(
                    e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                        e => e.Id.ShouldBe(processId),
                        e => e.StoppedByError.ShouldBeFalse(),
                        e => e.RetryCount.ShouldBe<short?>(1),
                        e => e.Status.ShouldBe(Model.Abstract.ProcessModule.Dto.ProcessStatusEnum.WaitEvent)
                        )
                    );

                triggers.ShouldSatisfyAllConditions(
                    e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                        e => e.ProcessId.ShouldBe(processId),
                        e => e.IsCompleted.ShouldBeFalse(),
                        e => e.IsActivated.ShouldBeTrue(),
                        e => e.Counter.ShouldBeNull()
                        )
                    );

                await dbContext.Set<TriggerDbEntity<Guid>>()
                    .Where(e => e.ProcessId == processId)
                    .ExecuteUpdateAsync(e => e.SetProperty(e => e.TimerDate, DateTimeOffset.MinValue));
            }
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var triggerService = scope.ServiceProvider.GetRequiredService<ITriggerRunner>();

                await triggerService.DbWorkAsync(true, default);

                var processes = await dbContext.Set<ProcessDbEntity<Guid>>()
                    .AsNoTracking()
                    .ToArrayAsync();
                var triggers = await dbContext.Set<TriggerDbEntity<Guid>>()
                    .AsNoTracking()
                    .ToArrayAsync();

                processes.ShouldSatisfyAllConditions(
                    e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                        e => e.Id.ShouldBe(processId),
                        e => e.StoppedByError.ShouldBeFalse(),
                        e => e.RetryCount.ShouldBe<short?>(1),
                        e => e.Status.ShouldBe(Model.Abstract.ProcessModule.Dto.ProcessStatusEnum.AsyncExecute)
                        )
                    );

                triggers.ShouldSatisfyAllConditions(
                    e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                        e => e.ProcessId.ShouldBe(processId),
                        e => e.IsCompleted.ShouldBeTrue(),
                        e => e.IsActivated.ShouldBeFalse(),
                        e => e.Counter.ShouldBeNull()
                        )
                    );

                await dbContext.Set<TriggerDbEntity<Guid>>()
                    .Where(e => e.Id == triggers[0].Id)
                    .ExecuteDeleteAsync();
            }

            //// 2)
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var runner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await runner.BuildHandler();

                await runner.RunAsync(oneCycle: true, default);
                await runner.WaitRunningTasksAsync(default);

                var processes = await dbContext.Set<ProcessDbEntity<Guid>>()
                    .AsNoTracking()
                    .ToArrayAsync();
                var triggers = await dbContext.Set<TriggerDbEntity<Guid>>()
                    .AsNoTracking()
                    .ToArrayAsync();

                processes.ShouldSatisfyAllConditions(
                    e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                        e => e.Id.ShouldBe(processId),
                        e => e.StoppedByError.ShouldBeFalse(),
                        e => e.RetryCount.ShouldBe<short?>(2),
                        e => e.Status.ShouldBe(Model.Abstract.ProcessModule.Dto.ProcessStatusEnum.WaitEvent)
                        )
                    );

                triggers.ShouldSatisfyAllConditions(
                    e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                        e => e.ProcessId.ShouldBe(processId),
                        e => e.IsCompleted.ShouldBeFalse(),
                        e => e.IsActivated.ShouldBeTrue(),
                        e => e.Counter.ShouldBeNull()
                        )
                    );

                await dbContext.Set<TriggerDbEntity<Guid>>()
                    .Where(e => e.ProcessId == processId)
                    .ExecuteUpdateAsync(e => e.SetProperty(e => e.TimerDate, DateTimeOffset.MinValue));
            }
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var triggerService = scope.ServiceProvider.GetRequiredService<ITriggerRunner>();

                await triggerService.DbWorkAsync(true, default);

                var processes = await dbContext.Set<ProcessDbEntity<Guid>>()
                    .AsNoTracking()
                    .ToArrayAsync();
                var triggers = await dbContext.Set<TriggerDbEntity<Guid>>()
                    .AsNoTracking()
                    .ToArrayAsync();

                processes.ShouldSatisfyAllConditions(
                    e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                        e => e.Id.ShouldBe(processId),
                        e => e.StoppedByError.ShouldBeFalse(),
                        e => e.RetryCount.ShouldBe<short?>(2),
                        e => e.Status.ShouldBe(Model.Abstract.ProcessModule.Dto.ProcessStatusEnum.AsyncExecute)
                        )
                    );

                triggers.ShouldSatisfyAllConditions(
                    e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                        e => e.ProcessId.ShouldBe(processId),
                        e => e.IsCompleted.ShouldBeTrue(),
                        e => e.IsActivated.ShouldBeFalse(),
                        e => e.Counter.ShouldBeNull()
                        )
                    );

                await dbContext.Set<TriggerDbEntity<Guid>>()
                    .Where(e => e.Id == triggers[0].Id)
                    .ExecuteDeleteAsync();
            }

            //// 3)
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var runner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();
                await runner.RunAsync(oneCycle: true, default);
                await runner.WaitRunningTasksAsync(default);

                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var processes = await dbContext.Set<ProcessDbEntity<Guid>>()
                    .AsNoTracking()
                    .ToArrayAsync();
                var triggers = await dbContext.Set<TriggerDbEntity<Guid>>()
                    .AsNoTracking()
                    .ToArrayAsync();

                processes.ShouldSatisfyAllConditions(
                    e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                        e => e.Id.ShouldBe(processId),
                        e => e.StoppedByError.ShouldBeTrue(),
                        e => e.RetryCount.ShouldBe<short?>(2),
                        e => e.Status.ShouldBe(Model.Abstract.ProcessModule.Dto.ProcessStatusEnum.WaitEvent)
                        )
                    );

                triggers.ShouldBeEmpty();
            }
        }


        private ValueTask Handler(
            IServiceProvider serviceProvider,
            ExecuteStepByStepGroupMiddleware<Guid>.ExecuteGroup group)
        {
            throw new Exception("Test exception");
        }
    }
}
