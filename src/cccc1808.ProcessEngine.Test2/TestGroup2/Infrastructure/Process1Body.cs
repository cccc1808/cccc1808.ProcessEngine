using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;

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

        public override ValueTask StepRangeAsync(
            ExecuteStepByStepGroupMiddleware<Guid>.ExecuteGroup group, CancellationToken cancellationToken)
        {
            var process = group.Group.Values.Single();

            switch (process.Process.Info.ProcessType.ProcessType)
            {
                case 1:
                    {
                        var setter = _serviceProvider.GetRequiredService<IProcessSetter>();
                        setter.SetStatus(
                            group.Group.Values.First(),
                            ProcessStatusEnum.Complete);

                        break;
                    }

                case 2:
                    {
                        throw new Exception("Test exception");

                        break;
                    }

                default: throw new NotImplementedException();
            }            

            return ValueTask.CompletedTask;
        }
    }
}
