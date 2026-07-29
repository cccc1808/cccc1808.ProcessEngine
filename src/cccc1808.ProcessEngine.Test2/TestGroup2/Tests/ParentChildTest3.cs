using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.ExternalCounter;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Test2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Tests
{
    /// <summary>
    /// /// <summary>
    /// https://wiki.denhome.ru/bin/view/Проекты%20и%20репозитории/Библиотеки/Движок%20cccc1808.%20ProcessEngine/Примеры/
    /// Пример 1. Вариант 5.
    /// </summary>
    /// </summary>
    [Collection(FixtureCollection.Name)]
    public class ParentChildTest3
        : IAsyncLifetime
    {
        private readonly FixtureCollection.Fixture _fixture;
        private readonly TestService _testService;

        public ParentChildTest3(
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
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Fact(Timeout = FixtureCollection.TestTimeout)]
        public async Task Test1()
        {
            var idGenerator = _fixture.ServiceProvider.GetRequiredService<IIdGenerator<Guid>>();

            var processId = await idGenerator.NextAsync(default);
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var testState = scope.ServiceProvider.GetRequiredService<TestProcessBody.TestState>();
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
                dbContext.Set<ProcessWakeupDbEntity<Guid>>().Add(
                    new ProcessWakeupDbEntity<Guid>(
                        await idGenerator.NextAsync(default),
                        processId,
                        isAsyncExecuting: true));

                await dbContext.SaveChangesAsync(default);

                testState.StepRange = Handler;
            }

            // 1) Выполняется родительский процесс.
            // Создается триггер (если пакетно, то отдельной транзакций).
            // Создаются и запускаются дочерние процессы.
            Guid childProcessId;
            string parentTriggerKey;
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunProcessRunnerAsync(scope.ServiceProvider);

                // assert.
                {
                    var childProcessData = await _testService.LoadAsync<ChildProcessDbEntity>(scope.ServiceProvider);
                    var allProceses = await _testService.LoadProcessAsync(scope.ServiceProvider);
                    var triggers = await _testService.LoadTriggersAsync(scope.ServiceProvider);

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
                            e => e.SignalCounter1.ShouldBe(0),
                            e => e.StreamProcessIsWaiting.ShouldBe(false),
                            e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeFalse(),
                            e => e.IsActivated.ShouldBeFalse(),
                            e => e.IsCompleted.ShouldBeFalse()));
                }
            }

            // 2) Обработка событий триггеров.
            // StreamTrigger фиксирует, что родительский процес уснул.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunTriggerConsumerRunnerAsync(scope.ServiceProvider);

                // assert.
                {
                    var triggers = await _testService.LoadTriggersAsync(scope.ServiceProvider);
                    triggers.ShouldSatisfyAllConditions(
                        e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                            e => e.Key.ShouldBe(parentTriggerKey),
                            e => e.SignalCounter1.ShouldBe(0),
                            e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeTrue(),
                            e => e.IsActivated.ShouldBeFalse(),
                            e => e.IsCompleted.ShouldBeFalse()));
                }
            }

            // 3) Выполнение дочерних процессов.
            // По завершению на родитедьский триггер публикуется событие.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunProcessRunnerAsync(scope.ServiceProvider);

                // assert.
                {
                    var childProcessData = await _testService.LoadAsync<ChildProcessDbEntity>(scope.ServiceProvider);
                    var allProceses = await _testService.LoadProcessAsync(scope.ServiceProvider);
                    var triggers = await _testService.LoadTriggersAsync(scope.ServiceProvider);

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
                            e => e.SignalCounter1.ShouldBe(0),
                            e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeTrue(),
                            e => e.IsActivated.ShouldBeFalse(),
                            e => e.IsCompleted.ShouldBeFalse()));
                }
            }

            // 4) Обработка событий триггеров.
            // События по SimpleStreamTrigger, активация триггера.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunTriggerConsumerRunnerAsync(scope.ServiceProvider);

                // assert.
                {
                    var triggers = await _testService.LoadTriggersAsync(scope.ServiceProvider);
                    triggers.ShouldSatisfyAllConditions(
                        e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                            e => e.Key.ShouldBe(parentTriggerKey),
                            e => e.SignalCounter1.ShouldBe(0),
                            e => e.StreamProcessIsWaiting.ShouldNotBeNull().ShouldBeFalse(),
                            e => e.IsActivated.ShouldBeTrue(),
                            e => e.IsCompleted.ShouldBeFalse()));
                }
            }

            // 5) Обработка активных триггеров. SimpleStreamTrigger.
            // Пробуждение родительского процесса.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunTriggerDbRunnerAsync(scope.ServiceProvider);

                // assert.
                {
                    var allProceses = await _testService.LoadProcessAsync(scope.ServiceProvider);
                    var triggers = await _testService.LoadTriggersAsync(scope.ServiceProvider);

                    allProceses.ShouldSatisfyAllConditions(
                        e => e.Length.ShouldBe(2),
                        e => e.ShouldContain(e => e.Id == childProcessId),
                        e => e.Single(e => e.Id == childProcessId).ShouldSatisfyAllConditions(
                            e => e.Status.ShouldBe(ProcessStatusEnum.Complete)),
                         e => e.Single(e => e.Id == processId).ShouldSatisfyAllConditions(
                            e => e.Status.ShouldBe(ProcessStatusEnum.AsyncExecute))
                        );

                    triggers.ShouldBeEmpty();
                }
            }

            // 6) Выполняется родительский процесс.
            // Завершение родительского процесса.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                await _testService.RunProcessRunnerAsync(scope.ServiceProvider);

                // assert.
                {
                    var allProceses = await _testService.LoadProcessAsync(scope.ServiceProvider);

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

        private static async ValueTask Handler(
            IServiceProvider serviceProvider,
            ExecuteStepByStepGroupMiddleware<Guid>.ExecuteGroup group)
        {
            var process = group.Group.Values.First();

            switch (process.Process.Info.ProcessType.ProcessType)
            {
                case 3:
                    {
                        var idGenerator = serviceProvider.GetRequiredService<IIdGenerator<Guid>>();
                        var triggerRepository = serviceProvider.GetRequiredService<ITriggerRepository<Guid>>();
                        var triggerOptions = serviceProvider.GetRequiredService<TriggerRunner<Guid>.OptionsDto>();
                        var dbcontext = serviceProvider.GetRequiredService<IEFDbContext>();
                        var setter = serviceProvider.GetRequiredService<IProcessSetter>();
                        var externalCounterProvider = serviceProvider.GetRequiredService<IExternalCounterProvider>();

                        var childProcessesCreated = await dbcontext
                            .Set<ChildProcessDbEntity>()
                            .Where(e => e.ParentProcessId == process.Id)
                            .AnyAsync();

                        if (!childProcessesCreated)
                        {
                            var childCount = 1;
                            var triggerKey = Guid.NewGuid().ToString();

                            await triggerRepository.CreateTriggerAsync(
                                ITriggerRepository<Guid>.CreateTriggerDto.SimpleStreamTrigger(
                                    triggerKey,
                                    DateTimeOffset.MinValue,
                                    process.Id,
                                    isRangeTrigger: true,
                                    ParentProcessTriggerHandler.Name,
                                    1,
                                    false,
                                    streamProcessIsWaiting: false,
                                    newSignalCounter: 0,
                                    isChildTrigger: false), 
                                CancellationToken.None);

                            await externalCounterProvider.CreateCounterAsync(triggerKey, childCount, default);

                            for (int i = 0; i < childCount; i++)
                            {
                                var processId = await idGenerator.NextAsync(default);
                                dbcontext.Set<ProcessDbEntity<Guid>>().Add(
                                    new ProcessDbEntity<Guid>(
                                        processId,
                                        4,
                                        1,
                                        1,
                                        DateTimeOffset.MinValue,
                                        false,
                                        ProcessStatusEnum.AsyncExecute,
                                        null
                                        ));
                                dbcontext.Set<ChildProcessDbEntity>().Add(
                                    new ChildProcessDbEntity(
                                        processId,
                                        process.Id,
                                        process.Id,
                                        triggerKey));

                                setter.SetStatus(
                                    process,
                                    ProcessStatusEnum.WaitEvent);

                                process.AddComponent<IStreamTriggerComponent>(
                                    new StreamTriggerComponent(
                                        triggerOptions.TriggerEventQueues.Single().QueueName, 
                                        [triggerKey])
                                    );
                            }
                        }
                        else
                        {
                            setter.SetStatus(
                                process,
                                ProcessStatusEnum.Complete);

                            // TODO: можно в самом триггере или тут.
                            // await externalCounterProvider.RemoveCounterAsync(triggerKey, default);
                        }

                        break;
                    }

                case 4:
                    {
                        var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
                        var isolationService = serviceProvider.GetRequiredService<IIsolationService>();
                        var setter = serviceProvider.GetRequiredService<IProcessSetter>();
                        var triggerOptions = serviceProvider.GetRequiredService<TriggerRunner<Guid>.OptionsDto>();
                        var triggerEventRaiser = serviceProvider.GetRequiredService<ITriggerEventRaiser<Guid>>();
                        var externalCounterProvider = serviceProvider.GetRequiredService<IExternalCounterProvider>();

                        var processIdString = process.Id.ToString();
                        var component = process.GetComponent<ChildProcessDbEntity>();

                        var counterCorrupted = !await externalCounterProvider.CounterExists(component.ParentTriggerKey, default);

                        if (!counterCorrupted)
                        {
                            // Счетчик существует.

                            // 1) Если текущий процес уже менял счетчик, то сбрасываем это.
                            await externalCounterProvider.CompensateCounterAsync(component.ParentTriggerKey, processIdString);

                            // 2) Меняем значение счетчика и фиксируем процесс.
                            var counter = await externalCounterProvider.TryDecrementCounterAsync(
                                component.ParentTriggerKey,
                                processIdString);

                            transactionManager.TryGetCurrentTransaction(out var currentTransaction);

                            isolationService.RegisterManualCompensate(
                                async (t) => 
                                {
                                    await externalCounterProvider.CompensateCounterAsync(component.ParentTriggerKey, processIdString);
                                });
                            currentTransaction.AddAfterCommitHandler(
                                // В случае коммита транзакции удаляем отметку участника счетчика.
                                commitHandler: async (t) => await externalCounterProvider.CommitCounterAsync(component.ParentTriggerKey, processIdString),
                                // В случае падения транзакции пробуем сбросить.
                                roolbackHandler: async (t) => await externalCounterProvider.CompensateCounterAsync(component.ParentTriggerKey, processIdString));

                            if (counter == 0)
                            {
                                // Если счетчик исчерпан, то шлем событие на триггер.
                                await triggerEventRaiser.RaiseAsync(
                                    [new ITriggerEventRaiser<Guid>.RaiseContainer(
                                    triggerOptions.TriggerEventQueues.Single().QueueName,
                                    component.ParentProcessId,
                                    new SignalSimpleStreamTriggerEvent(component.ParentTriggerKey)
                                    )],
                                    default);
                            }
                            else if (counter < 0)
                            {
                                counterCorrupted = true;
                            }
                        }

                        if (counterCorrupted)
                        {
                            // Счетчик был потерян (падение хранилища) или поврежден.

                            // Тогда просто публикуем событие на триггер и timeout т.к. мы не можем использовать счетчик
                            // и теперь триггер обязан каждый раз првоерять условие.
                            await triggerEventRaiser.RaiseAsync(
                                [new ITriggerEventRaiser<Guid>.RaiseContainer(
                                    // Для этой ветки можно использовать очередь с большей задержкой (окном аггрегации).
                                    triggerOptions.TriggerEventQueues.Single().QueueName,
                                    component.ParentProcessId,
                                    new SignalSimpleStreamTriggerEvent(component.ParentTriggerKey)
                                    )],
                                default);
                            
                            // TODO можно добавить timeout тут, если значение меньше указанного. Но также timeout в хендлере триггера после активации.
                        }

                        setter.SetStatus(
                            process,
                            ProcessStatusEnum.Complete);

                        // Убираем блокирующий ключ, чтобы условие выполнялось.
                        component.ActiveParentProcessId = null;

                        break;
                    }

                default: throw new NotImplementedException();
            }
        }
    }
}
