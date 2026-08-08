using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Entities;
using cccc1808.ProcessEngine.Model.StaticInstance.Implementation.Services;
using cccc1808.ProcessEngine.Test2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup6.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup6
{
    [Collection(FixtureCollection.Name)]
    public class StaticInstanceTest
        : IAsyncLifetime
    {
        private readonly FixtureCollection.Fixture _fixture;
        private readonly TestService _testService;

        public StaticInstanceTest(
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
        public async Task CreateTest()
        {
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var runner = scope.ServiceProvider.GetRequiredService<StaticInstanceRunner>();
                await runner.RunAsync(CancellationToken.None);
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var deploy = await _testService.LoadAsync<StaticInstanceDeployDbEntity>(scope.ServiceProvider);
                deploy.ShouldHaveSingleItem()
                    .ShouldSatisfyAllConditions(
                        e => e.Version.ShouldBe((short)1));

                var registration = await _testService.LoadAsync<StaticInstanceRegistrationDbEntity<Guid>>(scope.ServiceProvider);
                registration.ShouldHaveSingleItem()
                    .ShouldSatisfyAllConditions(
                        e => e.ProcessType.ShouldBe(1),
                        e => e.InstanceKey.ShouldBe(string.Empty));

                var processes = await _testService.LoadProcessAsync(scope.ServiceProvider);
                processes.ShouldHaveSingleItem()
                    .ShouldSatisfyAllConditions(
                        e => e.Id.ShouldBe(registration.First().ProcessId),
                        e => e.Status.ShouldBe(ProcessStatusEnum.AsyncExecute));
            }
        }

        [Fact(Timeout = FixtureCollection.TestTimeout)]
        public async Task UpdateEmptyTest()
        {
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                db.Set<StaticInstanceDeployDbEntity>().Add(
                    new StaticInstanceDeployDbEntity(
                        0, 
                        0
                        )
                    );
                await db.SaveChangesAsync(CancellationToken.None);
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var runner = scope.ServiceProvider.GetRequiredService<StaticInstanceRunner>();
                await runner.RunAsync(CancellationToken.None);
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var deploy = await _testService.LoadAsync<StaticInstanceDeployDbEntity>(scope.ServiceProvider);
                deploy.ShouldHaveSingleItem()
                    .ShouldSatisfyAllConditions(
                        e => e.Version.ShouldBe((short)1));

                var registration = await _testService.LoadAsync<StaticInstanceRegistrationDbEntity<Guid>>(scope.ServiceProvider);
                registration.ShouldHaveSingleItem()
                    .ShouldSatisfyAllConditions(
                        e => e.ProcessType.ShouldBe(1),
                        e => e.InstanceKey.ShouldBe(string.Empty));

                var processes = await _testService.LoadProcessAsync(scope.ServiceProvider);
                processes.ShouldHaveSingleItem()
                    .ShouldSatisfyAllConditions(
                        e => e.Id.ShouldBe(registration.First().ProcessId),
                        e => e.Status.ShouldBe(ProcessStatusEnum.AsyncExecute));
            }
        }
    }
}
