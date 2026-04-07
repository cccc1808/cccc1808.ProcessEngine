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
        private readonly TestState _testState;

        public Process1Body(
            IServiceProvider serviceProvider,
            IProcessRepository<Guid> repository,
            ITriggerRepository<Guid> triggerRepository,
            IProcessSetter processSetter
,
            TestState testState) : base(
                repository,
                triggerRepository,
                processSetter)
        {
            _serviceProvider = serviceProvider;
            _testState = testState;
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
            await _testState.StepRange(
                _serviceProvider, 
                group);
        }


        public class TestState 
        {
            public Func<IServiceProvider, ExecuteStepByStepGroupMiddleware<Guid>.ExecuteGroup, ValueTask> StepRange { get; set; }
                = null;
        }
    }
}
