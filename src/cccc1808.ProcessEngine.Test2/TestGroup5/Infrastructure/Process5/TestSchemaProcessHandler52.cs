using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Handlers;
using cccc1808.ProcessEngine.Test2.Infrastructure.ParentChild.Entities;

namespace cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process5
{
    internal class TestSchemaProcessHandler52 
        : BaseSchemaProcessHandler<Guid>
    {
        public static ProcessSchemaDto Schema { get; }
            = new ProcessSchemaDto(
                "1",
                [
                    new TokenDto(
                        "1",
                        new ServiceTaskTokenAction("1", "1_Execute")
                        {
                            Name = "Логика дочернего процесса",
                            ActivatedOnStart = true,
                            Transition = ITokenAction.TransitionDto.Complete(),
                        }
                        )
                    {
                        Name = "Дочерний процесс",
                    },
                ]
                )
            {
            };

        public static ProcessTypeDto ProcessType { get; }
            = new ProcessTypeDto(52, 1);

        public static bool UseSignalCode => false;

        private readonly ITriggerEventRaiser<Guid> _eventRaiser;

        public TestSchemaProcessHandler52(
            ITriggerEventRaiser<Guid> eventRaiser) :
            base()
        {
            _eventRaiser = eventRaiser;

            RegistryServiceTask("1_Execute", HandlerAsync);            
        }

        #region handlers

        private async ValueTask<ISchemaProcessHandler.ExecuteServiceTaskResult> HandlerAsync(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            var parentChild = parameters.process.GetComponent<ParentChildProcessDbEntity>();

            // Дочерний процесс выполнен, оповещает родительский процесс.
            parentChild.IsActive = false;
            await _eventRaiser.RaiseAsync(
                [new ITriggerEventRaiser<Guid>.RaiseContainer(
                    FixtureCollection.TriggerEvents,
                    parentChild.ProcessId,
                    new CounterTriggerEvent(parentChild.TriggerKey, -1))], 
                cancellationToken);            

            return ISchemaProcessHandler.ExecuteServiceTaskResult.Result(isComplete: true);
        }

        #endregion
    }
}
