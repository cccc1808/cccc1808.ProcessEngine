using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Tests
{
    [Collection(FixtureCollection.Name)]
    public class SimpleProcessTest
        : IAsyncLifetime
    {
        private readonly FixtureCollection.Fixture _fixture;

        public SimpleProcessTest(
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
        public async Task Q1()
        {
            var idGenerator = _fixture.ServiceProvider.GetRequiredService<IIdGenerator<Guid>>();
            var processId = await idGenerator.NextAsync(default);

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var runner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await runner.BuildHandler();

                dbContext.Set<ProcessDbEntity<Guid>>().Add(
                    new ProcessDbEntity<Guid>(
                        processId,
                        1,
                        1,
                        1,
                        DateTimeOffset.MinValue,
                        false,
                        Model.Abstract.ProcessModule.Dto.ProcessStatusEnum.AsyncExecute,
                        null
                        )
                    );
                await dbContext.SaveChangesAsync(default);

                await runner.RunAsync(oneCycle: true, default);
                await runner.WaitRunningTasksAsync(default);
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var processes = await dbContext.Set<ProcessDbEntity<Guid>>()
                    .AsNoTracking()
                    .ToArrayAsync();

                processes.ShouldSatisfyAllConditions(
                    e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                        e => e.Id.ShouldBe(processId),
                        e => e.StoppedByError.ShouldBeFalse(),
                        e => e.RetryCount.ShouldBeNull(),
                        e => e.Status.ShouldBe(Model.Abstract.ProcessModule.Dto.ProcessStatusEnum.Complete)
                        )
                    );
            }
        }        
    }
}
