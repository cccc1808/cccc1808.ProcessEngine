using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.ClassifierModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Services;
using cccc1808.ProcessEngine.Test2.TestGroup4.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup4.Infrastructure.Services;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup4
{
    [Collection(FixtureCollection.Name)]
    public class OutboxTest 
        : IAsyncLifetime
    {
        private readonly FixtureCollection.Fixture _fixture;

        public OutboxTest(
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
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
                var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                var outboxRaiser = scope.ServiceProvider.GetRequiredService<IOutboxSender<Guid>>();

                await using (var transaction = await transactionManager.StartTransactionAsync(default))
                {
                    await outboxRaiser.SendAsync(
                        [
                            (
                                new AggregateDto("0", "0"),
                                new MessageDto(
                                    "1",
                                    FixtureCollection.OutboxQueue,
                                    [],
                                    JsonSerializer.SerializeToDocument(new Message1Dto(){ BuisnessEntityId = Guid.NewGuid() }).RootElement.Clone(),
                                    -1)
                                ),                                
                            (
                                new AggregateDto("0", "0"),
                                new MessageDto(
                                    "1",
                                    FixtureCollection.OutboxQueue,
                                    [],
                                    JsonSerializer.SerializeToDocument(new Message1Dto(){ BuisnessEntityId = Guid.NewGuid() }).RootElement.Clone(),
                                    -1)
                                ),                                
                            (
                                new AggregateDto("0", "1"),
                                new MessageDto(
                                    "1",
                                    FixtureCollection.OutboxQueue,
                                    [],
                                    JsonSerializer.SerializeToDocument(new Message1Dto(){ BuisnessEntityId = Guid.NewGuid() }).RootElement.Clone(),
                                    -1)
                                ),
                            ],
                        default);

                    await dbContext.SaveChangesAsync(default);
                    await transaction.CommitAsync(default);
                }
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
                var queueProviderFactory = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();
                var triggerRunnern = scope.ServiceProvider.GetRequiredService<ITriggerRunner>();

                await triggerRunnern.DbWorkAsync(executeOne: true, default);
                await triggerRunnern.DbWorkAsync(executeOne: true, default);
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var processRunner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await processRunner.RunAsync(oneCycle: true, default);
                await processRunner.WaitRunningTasksAsync(default);
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var processRunner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await processRunner.RunAsync(oneCycle: true, default);
                await processRunner.WaitRunningTasksAsync(default);
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var queueProviderFactory = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();
                var consumer = await queueProviderFactory.GetConsumerAsync(FixtureCollection.OutboxQueue, default);

                var messages = await consumer.ConsumeBatchAsync(3, TimeSpan.FromSeconds(1), default);
                await consumer.CommitAsync(default);
                await queueProviderFactory.DisconnectConsumerAsync(FixtureCollection.OutboxQueue, default);

                messages.ShouldSatisfyAllConditions(
                    e => e.Count.ShouldBe(3)
                    );
            }
        }
    }
}
