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
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Test2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Tests
{
    [Collection(FixtureCollection.Name)]
    public class SimpleProcessTest
        : IAsyncLifetime
    {
        private readonly FixtureCollection.Fixture _fixture;
        private readonly TestService _testService;

        public SimpleProcessTest(
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

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var testState = scope.ServiceProvider.GetRequiredService<TestProcessBody.TestState>();
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

                testState.StepRange = Handler;
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunProcessRunnerAsync(scope.ServiceProvider);
            }
            
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var processes = await _testService.LoadProcessAsync(scope.ServiceProvider);

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
