using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        [Fact(Timeout = FixtureCollection.TestTimeout)]
        public async Task Test()
        {
            var watches = new Dictionary<string, Stopwatch>();

            var beId1 = Guid.NewGuid();
            var beId2 = Guid.NewGuid();

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("1", Stopwatch.StartNew());

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

                watches["1"].Stop();
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("2", Stopwatch.StartNew());

                var queueProviderFactory = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();
                var runner = scope.ServiceProvider.GetRequiredService<IInboxRunner>();

                await runner.StartAsync(oneCycle: true);
                await runner.WaitRunningTasksAsync(default);
                (await queueProviderFactory.DisconnectConsumerAsync(FixtureCollection.InboxQueue, default)).ShouldBeTrue();

                watches["2"].Stop();
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("3", Stopwatch.StartNew());

                var queueProviderFactory = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();
                var triggerRunnern = scope.ServiceProvider.GetRequiredService<ITriggerRunner>();

                await triggerRunnern.ConsumerWorkAsync(executeOne: true, default);
                (await queueProviderFactory.DisconnectConsumerAsync(FixtureCollection.TriggerQueue, default)).ShouldBeTrue();

                watches["3"].Stop();
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("4", Stopwatch.StartNew());

                var triggerRunnern = scope.ServiceProvider.GetRequiredService<ITriggerRunner>();

                await triggerRunnern.DbWorkAsync(executeOne: true, default);

                watches["4"].Stop();
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("5", Stopwatch.StartNew());

                var processRunner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await processRunner.RunAsync(oneCycle: true, default);
                await processRunner.WaitRunningTasksAsync(default);

                watches["5"].Stop();
            }

            // TODO: добавить промежуточные assert.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("6", Stopwatch.StartNew());

                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();

                var entities = await dbContext.Set<BuisnessDbEntity>().ToArrayAsync();
                entities.ShouldSatisfyAllConditions(
                    e => e.Length.ShouldBe(2),
                    e => e.ShouldContain(e => e.Id == beId1),
                    e => e.ShouldContain(e => e.Id == beId2),
                    e => e.First(e => e.Id == beId1).Counter.ShouldBe(2),
                    e => e.First(e => e.Id == beId2).Counter.ShouldBe(1));

                watches["6"].Stop();
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("7", Stopwatch.StartNew());

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

                watches["7"].Stop();
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("8", Stopwatch.StartNew());

                var queueProviderFactory = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();
                var runner = scope.ServiceProvider.GetRequiredService<IInboxRunner>();

                await runner.StartAsync(oneCycle: true);
                await runner.WaitRunningTasksAsync(default);
                (await queueProviderFactory.DisconnectConsumerAsync(FixtureCollection.InboxQueue, default)).ShouldBeTrue();

                watches["8"].Stop();
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("9", Stopwatch.StartNew());

                var queueProviderFactory = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();
                var triggerRunnern = scope.ServiceProvider.GetRequiredService<ITriggerRunner>();


                await triggerRunnern.ConsumerWorkAsync(executeOne: true, default);
                (await queueProviderFactory.DisconnectConsumerAsync(FixtureCollection.TriggerQueue, default)).ShouldBeTrue();

                watches["9"].Stop();
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("10", Stopwatch.StartNew());

                var triggerRunnern = scope.ServiceProvider.GetRequiredService<ITriggerRunner>();

                await triggerRunnern.DbWorkAsync(executeOne: true, default);

                watches["10"].Stop();
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("11", Stopwatch.StartNew());

                var processRunner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await processRunner.RunAsync(oneCycle: true, default);
                await processRunner.WaitRunningTasksAsync(default);

                watches["11"].Stop();
            }

            // TODO: добавить промежуточные assert.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("12", Stopwatch.StartNew());

                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();

                var entities = await dbContext.Set<BuisnessDbEntity>().ToArrayAsync();
                entities.ShouldSatisfyAllConditions(
                    e => e.Length.ShouldBe(2),
                    e => e.ShouldContain(e => e.Id == beId1),
                    e => e.ShouldContain(e => e.Id == beId2),
                    e => e.First(e => e.Id == beId1).Counter.ShouldBe(4),
                    e => e.First(e => e.Id == beId2).Counter.ShouldBe(2));

                watches["12"].Stop();
            }
        }
    }
}
