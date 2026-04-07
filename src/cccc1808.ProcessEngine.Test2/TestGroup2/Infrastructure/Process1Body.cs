using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.ChildProcess;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure
{
    internal class Process1Body : BaseRangeProcessHandler<Guid>
    {
        private readonly IServiceProvider _serviceProvider;

        public Process1Body(
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
            => new ExecuteStepByStepGroupMiddleware<Guid>.OptionsDto(
                10, 
                Model.Abstract.CommonModule.Storage.ChangesIsolation.IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker,
                true,
                false,
                true);

        public override async ValueTask StepRangeAsync(
            ExecuteStepByStepGroupMiddleware<Guid>.ExecuteGroup group, CancellationToken cancellationToken)
        {
            var process = group.Group.Values.Single();

            switch (process.Process.Info.ProcessType.ProcessType)
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
                        var idGenerator = _serviceProvider.GetRequiredService < IIdGenerator < Guid>> ();
                        var dbcontext = _serviceProvider.GetRequiredService<IEFDbContext>();
                        var setter = _serviceProvider.GetRequiredService<IProcessSetter>();

                        var childProcessesCreated = await dbcontext
                            .Set<ChildProcessDbEntity>()
                            .Where(e => e.ParentProcessId == process.Id)
                            .AnyAsync(cancellationToken);

                        if (!childProcessesCreated)
                        {
                            var childCount = 1;
                            var triggerKey = Guid.NewGuid().ToString();
                            dbcontext.Set<TriggerDbEntity<Guid>>().Add(new TriggerDbEntity<Guid>(
                                await idGenerator.NextAsync(cancellationToken),
                                triggerKey,
                                DateTimeOffset.MinValue,
                                DateTimeOffset.MinValue,
                                ParentProcessTriggerHandler.Name,
                                Model.Abstract.TriggerModule.Components.ITriggerComponent<Guid>.TriggerKind.Counter,
                                1,
                                false,
                                false,
                                process.Id,
                                childCount));

                            for (int i = 0; i < childCount; i++)
                            {
                                var processId = await idGenerator.NextAsync(cancellationToken);
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
                        var triggerEventRaiser = _serviceProvider.GetRequiredService<ITriggerEventRaiser>();

                        var component = process.GetComponent<ChildProcessDbEntity>();

                        // Оповещаем родительский процесс о завершении дочернего процесса.
                        await triggerEventRaiser.RaiseAsync(
                            [new TriggerEvent(component.ParentTriggerKey, ignoreDelay: false)], 
                            cancellationToken);

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
