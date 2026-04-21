using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Services;
using cccc1808.ProcessEngine.Model.Kafka.Implementation.QueueModule.Provider;
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

        [Fact(/*Timeout = FixtureCollection.TestTimeout*/)]
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


        [Fact]
        public async Task TTest()
        {
            var beId1 = Guid.NewGuid();
            var beId2 = Guid.NewGuid();

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var queueProviderFactory = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();
                var options = scope.ServiceProvider.GetRequiredService<KafkaQueueProviderFactory.OptionsDto>();

                options.PartitionCountFunc = (_) => 2;

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

                var messages = new MessageDto[] {
                    new MessageDto(Guid.NewGuid().ToString(), FixtureCollection.InboxQueue, [], System.Text.Json.JsonSerializer.SerializeToDocument(m1).RootElement.Clone(), 0),
                    new MessageDto(Guid.NewGuid().ToString(), FixtureCollection.InboxQueue, [], System.Text.Json.JsonSerializer.SerializeToDocument(m1).RootElement.Clone(), 0),
                    new MessageDto(Guid.NewGuid().ToString(), FixtureCollection.InboxQueue, [], System.Text.Json.JsonSerializer.SerializeToDocument(m1).RootElement.Clone(), 1),                
                };

                var producer = await queueProviderFactory.GetProducerAsync(FixtureCollection.InboxQueue, default);
                await producer.ProduceBatchAsync(
                    messages,
                    default);
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var store = new KafkaMessageStore(
                    host: scope.ServiceProvider.GetRequiredService<KafkaQueueProviderFactory.OptionsDto>().Host);

                var result1 = await store.GetMessagesAsync(
                    [
                        new IMessageStore.MessageIdDto(FixtureCollection.InboxQueue, null, 0, 0),
                        new IMessageStore.MessageIdDto(FixtureCollection.InboxQueue, null, 0, 1),
                        new IMessageStore.MessageIdDto(FixtureCollection.InboxQueue, null, 1, 0),
                        ],
                    default);
                var result2 = await store.GetMessagesAsync(
                    [
                        new IMessageStore.MessageIdDto(FixtureCollection.InboxQueue, null, 0, 1)
                        ],
                    default);

                var result3 = await store.GetMessagesAsync(
                    [
                        new IMessageStore.MessageIdDto(FixtureCollection.InboxQueue, null, 0, 10)
                        ],
                    default);
            }
        }
    }
}
