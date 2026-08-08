using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Services;
using cccc1808.ProcessEngine.Test2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup4.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup4.Infrastructure.Services;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup4
{
    [Collection(FixtureCollection.Name)]
    public class InboxTest
        : IAsyncLifetime
    {
        private readonly FixtureCollection.Fixture _fixture;
        private readonly TestService _testService;

        public InboxTest(
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
        public async Task Test()
        {
            var watches = new Dictionary<string, Stopwatch>();

            var beId1 = Guid.NewGuid();
            var beId2 = Guid.NewGuid();

            // 1) Создаем бизнес сущности, публикуем сообщения в inbox очереди.
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
                            JsonHelper.ToJsonElement(e),
                            Partition: -1))
                        .ToArray(),
                    default);

                watches["1"].Stop();
            }

            // 2) Запускаем Inbox runner (считывание сообщений, созранение в InbxoMessage и подача сигнала на триггер).
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

            // 3) Запускаем trigger consumer, активируется триггер на inbox процесс.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("3", Stopwatch.StartNew());

                await _testService.RunTriggerConsumerRunnerAsync(scope.ServiceProvider, withNotification: true);

                watches["3"].Stop();
            }

            // 4) Выполняется inbox trigger, пробуждается процесс.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("4", Stopwatch.StartNew());

                await _testService.RunTriggerExecuteRunnerAsync(
                    scope.ServiceProvider, 
                    withTriggerNotification: false, 
                    withProcessNotification: true);

                watches["4"].Stop();
            }

            // 5) Выпволняется inbox процесс, обрабатываются inbox message. 
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("5", Stopwatch.StartNew());

                await _testService.RunProcessRunnerAsync(scope.ServiceProvider);

                watches["5"].Stop();
            }

            // TODO: добавить промежуточные assert.
            // 7) Проверяем, что бизнес сущности обновлены.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("6", Stopwatch.StartNew());

                var entities = await _testService.LoadAsync<BuisnessDbEntity>(scope.ServiceProvider);
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
                            JsonHelper.ToJsonElement(e),
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

                await _testService.RunTriggerConsumerRunnerAsync(scope.ServiceProvider, withNotification: true);

                watches["9"].Stop();
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("10", Stopwatch.StartNew());

                await _testService.RunTriggerExecuteRunnerAsync(
                    scope.ServiceProvider, 
                    withTriggerNotification: false,
                    withProcessNotification: true);

                watches["10"].Stop();
            }

            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("11", Stopwatch.StartNew());

                await _testService.RunProcessRunnerAsync(scope.ServiceProvider);

                watches["11"].Stop();
            }

            // TODO: добавить промежуточные assert.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                watches.Add("12", Stopwatch.StartNew());

                var entities = await _testService.LoadAsync<BuisnessDbEntity>(scope.ServiceProvider);
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
