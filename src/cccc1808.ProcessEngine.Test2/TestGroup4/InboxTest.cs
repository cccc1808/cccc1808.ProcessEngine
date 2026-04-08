using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Services;
using cccc1808.ProcessEngine.Test2.TestGroup4.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup4.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup4
{
    [Collection(FixtureCollection.Name)]
    public class InboxTest
        : IAsyncLifetime
    {
        private readonly FixtureCollection.Fixture _fixture;

        public InboxTest(
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
        public async Task Test()
        {
            var beId1 = Guid.NewGuid();
            var beId2 = Guid.NewGuid();

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var queueProviderFactory = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();

                dbContext.Set<BuisnessDbEntity>().AddRange(
                    new BuisnessDbEntity() { Id = beId1, Counter = 0 },
                    new BuisnessDbEntity() { Id = beId2, Counter = 0 }
                    );
                await dbContext.SaveChangesAsync(default);

                var producer = await queueProviderFactory.GetProducerAsync(FixtureCollection.InboxQueue, default);

                var m1 = new Message1Dto()
                {
                    BuisnessEntityId = beId1,
                };
                var m2 = new Message1Dto()
                {
                    BuisnessEntityId = beId1,
                };
                var m3 = new Message1Dto()
                {
                    BuisnessEntityId = beId2,
                };

                await producer.ProduceBatchAsync(
                    new Message1Dto[] { m1, m2, m3 }
                        .Select(e => new MessageDto(
                            Guid.NewGuid().ToString(),
                            FixtureCollection.InboxQueue,
                            [],
                            System.Text.Json.JsonSerializer.SerializeToDocument(e).RootElement.Clone(),
                            Partition: -1))
                        .ToArray(),
                    default);
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var queueProviderFactory = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();
                var runner = scope.ServiceProvider.GetRequiredService<IInboxRunner>();

                await runner.StartAsync(oneCycle: true);
                await runner.WaitRunningTasksAsync(default);
                (await queueProviderFactory.DisconnectConsumerAsync(FixtureCollection.InboxQueue, default)).ShouldBeTrue();
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var queueProviderFactory = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();
                var triggerRunnern = scope.ServiceProvider.GetRequiredService<ITriggerRunner>();


                await triggerRunnern.ConsumerWorkAsync(executeOne: true, default);
                (await queueProviderFactory.DisconnectConsumerAsync(FixtureCollection.TriggerQueue, default)).ShouldBeTrue();
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var triggerRunnern = scope.ServiceProvider.GetRequiredService<ITriggerRunner>();

                await triggerRunnern.DbWorkAsync(executeOne: true, default);
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var processRunner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await processRunner.RunAsync(oneCycle: true, default);
                await processRunner.WaitRunningTasksAsync(default);
            }

            // TODO: добавить промежуточные assert.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();

                var entities = await dbContext.Set<BuisnessDbEntity>().ToArrayAsync();
                entities.ShouldSatisfyAllConditions(
                    e => e.Length.ShouldBe(2),
                    e => e.ShouldContain(e => e.Id == beId1),
                    e => e.ShouldContain(e => e.Id == beId2),
                    e => e.First(e => e.Id == beId1).Counter.ShouldBe(2),
                    e => e.First(e => e.Id == beId2).Counter.ShouldBe(1));
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var queueProviderFactory = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();
                var producer = await queueProviderFactory.GetProducerAsync(FixtureCollection.InboxQueue, default);

                var m1 = new Message1Dto()
                {
                    BuisnessEntityId = beId1,
                };
                var m2 = new Message1Dto()
                {
                    BuisnessEntityId = beId1,
                };
                var m3 = new Message1Dto()
                {
                    BuisnessEntityId = beId2,
                };

                await producer.ProduceBatchAsync(
                    new Message1Dto[] { m1, m2, m3 }
                        .Select(e => new MessageDto(
                            Guid.NewGuid().ToString(),
                            FixtureCollection.InboxQueue,
                            [],
                            System.Text.Json.JsonSerializer.SerializeToDocument(e).RootElement.Clone(),
                            Partition: -1))
                        .ToArray(),
                    default);
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var queueProviderFactory = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();
                var runner = scope.ServiceProvider.GetRequiredService<IInboxRunner>();

                await runner.StartAsync(oneCycle: true);
                await runner.WaitRunningTasksAsync(default);
                (await queueProviderFactory.DisconnectConsumerAsync(FixtureCollection.InboxQueue, default)).ShouldBeTrue();
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var queueProviderFactory = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();
                var triggerRunnern = scope.ServiceProvider.GetRequiredService<ITriggerRunner>();


                await triggerRunnern.ConsumerWorkAsync(executeOne: true, default);
                (await queueProviderFactory.DisconnectConsumerAsync(FixtureCollection.TriggerQueue, default)).ShouldBeTrue();
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var triggerRunnern = scope.ServiceProvider.GetRequiredService<ITriggerRunner>();

                await triggerRunnern.DbWorkAsync(executeOne: true, default);
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var processRunner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await processRunner.RunAsync(oneCycle: true, default);
                await processRunner.WaitRunningTasksAsync(default);
            }

            // TODO: добавить промежуточные assert.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();

                var entities = await dbContext.Set<BuisnessDbEntity>().ToArrayAsync();
                entities.ShouldSatisfyAllConditions(
                    e => e.Length.ShouldBe(2),
                    e => e.ShouldContain(e => e.Id == beId1),
                    e => e.ShouldContain(e => e.Id == beId2),
                    e => e.First(e => e.Id == beId1).Counter.ShouldBe(4),
                    e => e.First(e => e.Id == beId2).Counter.ShouldBe(2));
            }
        }
    }
}
