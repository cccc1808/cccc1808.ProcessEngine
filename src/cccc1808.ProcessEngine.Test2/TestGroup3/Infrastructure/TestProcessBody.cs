using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Test2.TestGroup3.Infrastructure
{
    internal class TestProcessBody : BaseRangeProcessHandler<Guid>
    {
        private readonly IServiceProvider _serviceProvider;

        public TestProcessBody(
            IServiceProvider serviceProvider,
            IProcessRepository<Guid> repository, 
            ITriggerRepository<Guid> triggerRepository,
            IProcessSetter processSetter
            ) : base(
                repository,
                triggerRepository,
                processSetter)
        {
            _serviceProvider = serviceProvider;
        }

        public override ExecuteStepByStepGroupMiddleware<Guid>.OptionsDto Options 
            => Presets<Guid>.Preset1;

        public override async ValueTask StepRangeAsync(
            ExecuteStepByStepGroupMiddleware<Guid>.ExecuteGroup group, CancellationToken cancellationToken)
        {
            foreach (var process in group.Group.Values)
            {
                switch (process.Process.Info.Registry.Unique.ProcessType.ProcessType)
                {
                    case 1:
                        {
                            var setter = _serviceProvider.GetRequiredService<IProcessSetter>();
                            setter.SetStatus(
                                process,
                                ProcessStatusEnum.Complete);

                            break;
                        }

                    case 2:
                        {
                            throw new Exception("Test exception");

                            break;
                        }

                    case 3:
                        {
                            var idGenerator = _serviceProvider.GetRequiredService<IIdGenerator<Guid>>();
                            var dbcontext = _serviceProvider.GetRequiredService<IEFDbContext>();
                            var triggerRepository = _serviceProvider.GetRequiredService<ITriggerRepository<Guid>>();
                            var setter = _serviceProvider.GetRequiredService<IProcessSetter>();

                            var childProcessesCreated = await dbcontext
                                .Set<ChildProcessDbEntity>()
                                .Where(e => e.ParentProcessId == process.Id)
                                .AnyAsync(cancellationToken);

                            if (!childProcessesCreated)
                            {
                                var childCount = FixtureCollection.RangeConst;
                                var triggerKey = Guid.NewGuid().ToString();

                                await triggerRepository.CreateTriggerAsync(
                                    ITriggerRepository<Guid>.CreateTriggerDto.CounterTrigger(
                                        triggerKey,
                                        DateTimeOffset.MinValue,
                                        process.Id,
                                        isRangeTrigger: true,
                                        ParentProcessTriggerHandler.Name,
                                        1,
                                        isActivated: false,
                                        counter: childCount,
                                        isChildTrigger: false),
                                    CancellationToken.None);

                                for (int i = 0; i < childCount; i++)
                                {
                                    var processId = await idGenerator.NextAsync(cancellationToken);
                                    dbcontext.Set<ProcessDbEntity<Guid>>().Add(
                                        new ProcessDbEntity<Guid>(
                                            processId,
                                            4,
                                            1,
                                            1,
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
                            var setter = _serviceProvider.GetRequiredService<IProcessSetter>();
                            var triggerOptions = _serviceProvider.GetRequiredService<TriggerRunner<Guid>.OptionsDto>();
                            var triggerEventRaiser = _serviceProvider.GetRequiredService<ITriggerEventRaiser<Guid>>();

                            var component = process.GetComponent<ChildProcessDbEntity>();

                            // Оповещаем родительский процесс о завершении дочернего процесса.
                            await triggerEventRaiser.RaiseAsync(
                                [new ITriggerEventRaiser<Guid>.RaiseContainer(
                                triggerOptions.Consumer_TriggerEventQueues.Single().QueueName,
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

        public override async Task SaveRangeAsync(
            ExecuteStepByStepGroupMiddleware<Guid>.ExecuteGroup group, 
            CancellationToken cancellationToken)
        {
            var e = _serviceProvider.GetRequiredService<IEFDbContext>().DbContext.ChangeTracker.Entries().ToArray();
            await base.SaveRangeAsync(group, cancellationToken);
        }
    }
}
