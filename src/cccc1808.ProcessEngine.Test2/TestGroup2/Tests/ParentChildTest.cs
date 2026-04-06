using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.ChildProcess;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Tests
{
    [Collection(FixtureCollection.Name)]
    public class ParentChildTest
        : IAsyncLifetime
    {
        private readonly FixtureCollection.Fixture _fixture;

        public ParentChildTest(
            FixtureCollection.Fixture fixture)
        {
            _fixture = fixture;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            await _fixture.CleanEnvironmentAsync();
        }

        [Fact]
        public async Task Test1()
        {
            var idGenerator = _fixture.ServiceProvider.GetRequiredService<IIdGenerator<Guid>>();

            var processId = await idGenerator.NextAsync(default);
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();

                dbContext.Set<ProcessDbEntity<Guid>>().Add(
                    new ProcessDbEntity<Guid>(
                        processId,
                        3,
                        1,
                        1,
                        DateTimeOffset.MinValue,
                        false,
                        ProcessStatusEnum.AsyncExecute,
                        null
                        )
                    );
                dbContext.Set<ProcessWakeUpDbEntity<Guid>>().Add(
                    new ProcessWakeUpDbEntity<Guid>(
                        await idGenerator.NextAsync(default),
                        processId,
                        isAsyncExecuting: true));

                await dbContext.SaveChangesAsync(default);
            }

            Guid childProcessId;
            string parentTriggerKey;
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var runner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await runner.RunAsync(oneCycle: true, default);
                await runner.WaitRunningTasksAsync(default);

                var childProcessData = await dbContext.Set<ChildProcessDbEntity>().AsNoTracking().ToArrayAsync();
                var allProceses = await dbContext.Set<ProcessDbEntity<Guid>>().AsNoTracking().ToArrayAsync();
                var triggers = await dbContext.Set<TriggerDbEntity<Guid>>().AsNoTracking().ToArrayAsync();

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
                        e => e.Counter.ShouldBe(1),
                        e => e.IsActivated.ShouldBeFalse(),
                        e => e.IsCompleted.ShouldBeFalse()));
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var runner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await runner.RunAsync(oneCycle: true, default);
                await runner.WaitRunningTasksAsync(default);

                var childProcessData = await dbContext.Set<ChildProcessDbEntity>().AsNoTracking().ToArrayAsync();
                var allProceses = await dbContext.Set<ProcessDbEntity<Guid>>().AsNoTracking().ToArrayAsync();
                var triggers = await dbContext.Set<TriggerDbEntity<Guid>>().AsNoTracking().ToArrayAsync();

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
                        e => e.Counter.ShouldBe(1),
                        e => e.IsActivated.ShouldBeFalse(),
                        e => e.IsCompleted.ShouldBeFalse()));
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var triggerOptions = scope.ServiceProvider.GetRequiredService<TriggerOptions>();
                var triggerService = scope.ServiceProvider.GetRequiredService<ITriggerService>();
                var queueProviderFactory = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();

                await triggerService.ConsumerWorkAsync(executeOne: true, default);
                (await queueProviderFactory.DisconnectConsumerAsync(triggerOptions.TriggerEventQueueName, default)).ShouldBeTrue();

                var triggers = await dbContext.Set<TriggerDbEntity<Guid>>().AsNoTracking().ToArrayAsync();
                triggers.ShouldSatisfyAllConditions(
                    e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                        e => e.Key.ShouldBe(parentTriggerKey),
                        e => e.Counter.ShouldBe(0),
                        e => e.IsActivated.ShouldBeTrue(),
                        e => e.IsCompleted.ShouldBeFalse()));
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var triggerService = scope.ServiceProvider.GetRequiredService<ITriggerService>();

                await triggerService.DbWorkAsync(true, default);

                var allProceses = await dbContext.Set<ProcessDbEntity<Guid>>().AsNoTracking().ToArrayAsync();
                var triggers = await dbContext.Set<TriggerDbEntity<Guid>>().AsNoTracking().ToArrayAsync();

                allProceses.ShouldSatisfyAllConditions(
                    e => e.Length.ShouldBe(2),
                    e => e.ShouldContain(e => e.Id == childProcessId),
                    e => e.Single(e => e.Id == childProcessId).ShouldSatisfyAllConditions(
                        e => e.Status.ShouldBe(ProcessStatusEnum.Complete)),
                     e => e.Single(e => e.Id == processId).ShouldSatisfyAllConditions(
                        e => e.Status.ShouldBe(ProcessStatusEnum.AsyncExecute))
                    );

                triggers.ShouldSatisfyAllConditions(
                    e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                        e => e.Key.ShouldBe(parentTriggerKey),
                        e => e.Counter.ShouldBe(0),
                        e => e.IsActivated.ShouldBeFalse(),
                        e => e.IsCompleted.ShouldBeTrue()));
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var runner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await runner.RunAsync(oneCycle: true, default);
                await runner.WaitRunningTasksAsync(default);

                var allProceses = await dbContext.Set<ProcessDbEntity<Guid>>().AsNoTracking().ToArrayAsync();

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
}
