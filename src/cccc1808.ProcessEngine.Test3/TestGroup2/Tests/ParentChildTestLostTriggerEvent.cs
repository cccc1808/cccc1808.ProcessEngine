using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services;

using LinqToDB;
using LinqToDB.Async;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Tests
{
    [Collection(FixtureCollection.Name)]
    public class ParentChildTestLostTriggerEvent
        : IAsyncLifetime
    {
        private readonly FixtureCollection.Fixture _fixture;

        public ParentChildTestLostTriggerEvent(
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
                var testState = scope.ServiceProvider.GetRequiredService<Process1Body.TestState>();
                var dbContext = scope.ServiceProvider.GetRequiredService<ILinq2DbDataConnection>();

                await dbContext.DataConnection.InsertAsync(
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
                await dbContext.DataConnection.InsertAsync(
                    new ParentProcessDataDbEntity(
                        await idGenerator.NextAsync(default),
                        processId));
                await dbContext.DataConnection.InsertAsync(
                    new ProcessWakeupDbEntity<Guid>(
                        await idGenerator.NextAsync(default),
                        processId,
                        isAsyncExecuting: true));

                testState.StepRange = Handler;
            }

            // Запуск родительского процесса - порожление дочерних процессов.
            Guid childProcessId;
            // string parentTriggerKey;
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ILinq2DbDataConnection>();
                var runner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await runner.RunAsync(oneCycle: true, default);
                await runner.WaitRunningTasksAsync(default);

                var childProcessData = await dbContext.Set<ChildProcessDbEntity>().ToArrayAsync();
                var allProceses = await dbContext.Set<ProcessDbEntity<Guid>>().ToArrayAsync();
                var triggers = await dbContext.Set<TriggerDbEntity<Guid>>().ToArrayAsync();

                childProcessData.ShouldSatisfyAllConditions(
                    e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                        e => e.ParentProcessId.ShouldBe(processId),
                        e => e.ActiveParentProcessId.ShouldBe(processId)));

                childProcessId = childProcessData.Single().ProcessId;
                // parentTriggerKey = childProcessData.Single().ParentTriggerKey;

                allProceses.ShouldSatisfyAllConditions(
                    e => e.Length.ShouldBe(2),
                    e => e.ShouldContain(e => e.Id == childProcessId),
                    e => e.Single(e => e.Id == childProcessId).ShouldSatisfyAllConditions(
                        e => e.Status.ShouldBe(ProcessStatusEnum.AsyncExecute)),
                     e => e.Single(e => e.Id == processId).ShouldSatisfyAllConditions(
                        e => e.Status.ShouldBe(ProcessStatusEnum.WaitEvent))
                    );
            }

            // Выполнение дочерних процессов - порождение триггера.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ILinq2DbDataConnection>();
                var runner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await runner.RunAsync(oneCycle: true, default);
                await runner.WaitRunningTasksAsync(default);

                var childProcessData = await dbContext.Set<ChildProcessDbEntity>().ToArrayAsync();
                var allProceses = await dbContext.Set<ProcessDbEntity<Guid>>().ToArrayAsync();
                var triggers = await dbContext.Set<TriggerDbEntity<Guid>>().ToArrayAsync();

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
            }

            // Создаем экстренный триггер для проверки его работы.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var triggerRepository = scope.ServiceProvider.GetRequiredService<ITriggerRepository<Guid>>();
                var dbContext = scope.ServiceProvider.GetRequiredService<ILinq2DbDataConnection>();

                await triggerRepository.CreateTriggerAsync(
                    ITriggerRepository<Guid>.CreateTriggerDto.TimerTrigger(
                        Guid.NewGuid().ToString(),
                        DateTimeOffset.MinValue,
                        Guid.Empty,
                        ParentProcessEmegencyTriggerHandler.Name,
                        1,
                        isActivated: true),
                    CancellationToken.None);
            }

            // Выполнение триггера - пробуждение родительского процесса.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ILinq2DbDataConnection>();
                var triggerOptions = scope.ServiceProvider.GetRequiredService<TriggerOptions<Guid>>();
                var triggerService = scope.ServiceProvider.GetRequiredService<ITriggerRunner>();
                var queueProviderFactory = scope.ServiceProvider.GetRequiredService<IQueueProviderFactory>();

                await triggerService.DbWorkAsync(executeOne: true, default);

                var triggers = await dbContext.Set<TriggerDbEntity<Guid>>().ToArrayAsync();
                triggers.ShouldSatisfyAllConditions(
                    e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                        // e => e.Key.ShouldBe(parentTriggerKey),
                        e => e.SignalCounter1.ShouldBeNull(),
                        e => e.IsActivated.ShouldBeTrue(),
                        e => e.IsCompleted.ShouldBeFalse()));
            }

            // Завершение родительского процесса.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ILinq2DbDataConnection>();
                var triggerService = scope.ServiceProvider.GetRequiredService<ITriggerRunner>();

                await triggerService.DbWorkAsync(true, default);

                var allProceses = await dbContext.Set<ProcessDbEntity<Guid>>().ToArrayAsync();
                var triggers = await dbContext.Set<TriggerDbEntity<Guid>>().ToArrayAsync();

                allProceses.ShouldSatisfyAllConditions(
                    e => e.Length.ShouldBe(2),
                    e => e.ShouldContain(e => e.Id == childProcessId),
                    e => e.Single(e => e.Id == childProcessId).ShouldSatisfyAllConditions(
                        e => e.Status.ShouldBe(ProcessStatusEnum.Complete)),
                     e => e.Single(e => e.Id == processId).ShouldSatisfyAllConditions(
                        e => e.Status.ShouldBe(ProcessStatusEnum.AsyncExecute))
                    );

                //triggers.ShouldSatisfyAllConditions(
                //    e => e.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                //        // e => e.Key.ShouldBe(parentTriggerKey),
                //        e => e.Counter.ShouldBe(0),
                //        e => e.IsActivated.ShouldBeFalse(),
                //        e => e.IsCompleted.ShouldBeTrue()));
            }

            // assert.
            await using (var scope = _fixture.ServiceProvider.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ILinq2DbDataConnection>();
                var runner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

                await runner.RunAsync(oneCycle: true, default);
                await runner.WaitRunningTasksAsync(default);

                var allProceses = await dbContext.Set<ProcessDbEntity<Guid>>().ToArrayAsync();

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

        private async ValueTask Handler(
            IServiceProvider serviceProvider,
            ExecuteStepByStepGroupMiddleware<Guid>.ExecuteGroup group)
        {
            var process = group.Group.Values.First();

            switch (process.Process.Info.ProcessType.ProcessType)
            {
                case 3:
                    {
                        var idGenerator = serviceProvider.GetRequiredService<IIdGenerator<Guid>>();
                        var dbcontext = serviceProvider.GetRequiredService<ILinq2DbDataConnection>();
                        var setter = serviceProvider.GetRequiredService<IProcessSetter>();

                        var childProcessesCreated = await dbcontext
                            .Set<ChildProcessDbEntity>()
                            .Where(e => e.ParentProcessId == process.Id)
                            .AnyAsync();

                        if (!childProcessesCreated)
                        {
                            var childCount = 1;
                            var triggerKey = Guid.NewGuid().ToString();

                            // Имитируем ситуацию потери TriggerEvent, не создаем триггер.
                            //dbcontext.Set<TriggerDbEntity<Guid>>().Add(new TriggerDbEntity<Guid>(
                            //    await idGenerator.NextAsync(default),
                            //    triggerKey,
                            //    DateTimeOffset.MinValue,
                            //    DateTimeOffset.MinValue,
                            //    ParentProcessTriggerHandler.Name,
                            //    Model.Abstract.TriggerModule.Components.ITriggerComponent<Guid>.TriggerKind.Counter,
                            //    1,
                            //    false,
                            //    false,
                            //    process.Id,
                            //    childCount));

                            for (int i = 0; i < childCount; i++)
                            {
                                var processId = await idGenerator.NextAsync(default);
                                await dbcontext.DataConnection.InsertAsync(
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
                                await dbcontext.DataConnection.InsertAsync(
                                    new ChildProcessDbEntity(
                                        processId,
                                        process.Id,
                                        process.Id,
                                        triggerKey));

                                setter.SetStatus(
                                    process,
                                    ProcessStatusEnum.WaitEvent);
                            }
                        }
                        else
                        {
                            setter.SetStatus(
                                process,
                                ProcessStatusEnum.Complete);
                        }

                        break;
                    }

                case 4:
                    {
                        var setter = serviceProvider.GetRequiredService<IProcessSetter>();
                        var triggerOptions = serviceProvider.GetRequiredService<TriggerRunner<Guid>.OptionsDto>();
                        var triggerEventRaiser = serviceProvider.GetRequiredService<ITriggerEventRaiser<Guid>>();

                        var component = process.GetComponent<ChildProcessDbEntity>();

                        // Оповещаем родительский процесс о завершении дочернего процесса.
                        await triggerEventRaiser.RaiseAsync(
                            [new ITriggerEventRaiser<Guid>.RaiseContainer(
                                triggerOptions.TriggerEventQueues.Single().QueueName,
                                component.ParentProcessId,
                                new CounterTriggerEvent(component.ParentTriggerKey, value: -1)
                                )],
                            default);

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
