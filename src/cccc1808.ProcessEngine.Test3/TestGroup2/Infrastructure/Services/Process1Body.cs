using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;

namespace cccc1808.ProcessEngine.Test3.TestGroup2.Infrastructure.Services
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
