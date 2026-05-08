using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services;
using LinqToDB;
using LinqToDB.Async;

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

        [Fact(Timeout = FixtureCollection.TestTimeout)]
        public async Task Test1()
        {
            var idGenerator = _fixture.ServiceProvider.GetRequiredService<IIdGenerator<Guid>>();
            var processId = await idGenerator.NextAsync(default);

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ILinq2DbDataConnection>();
                var testState = scope.ServiceProvider.GetRequiredService<Process1Body.TestState>();
                var runner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await runner.BuildHandler();
                await dbContext.DataConnection.InsertAsync(
                    new ProcessDbEntity<Guid>(
                        processId,
                        1,
                        1,
                        1,
                        DateTimeOffset.MinValue,
                        false,
                        ProcessStatusEnum.AsyncExecute,
                        null
                        )
                    );

                testState.StepRange = Handler;
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ILinq2DbDataConnection>();
                var testState = scope.ServiceProvider.GetRequiredService<Process1Body.TestState>();
                var runner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await runner.RunAsync(oneCycle: true, default);
                await runner.WaitRunningTasksAsync(default);
            }
            
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ILinq2DbDataConnection>();
                var processes = await dbContext.Set<ProcessDbEntity<Guid>>()
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


        private ValueTask Handler(
            IServiceProvider serviceProvider, 
            ExecuteStepByStepGroupMiddleware<Guid>.ExecuteGroup group) 
        {
            var setter = serviceProvider.GetRequiredService<IProcessSetter>();
            setter.SetStatus(
                group.Group.Values.First(),
                ProcessStatusEnum.Complete);

            return ValueTask.CompletedTask;
        }
    }
}
