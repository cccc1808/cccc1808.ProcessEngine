using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Handlers;
using cccc1808.ProcessEngine.Model.SimpleSchema.Implementation.Handlers;

namespace cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process1
{
    internal class TestSchemaProcessHandler : BaseSchemaProcessHandler<Guid>
    {
        public TestSchemaProcessHandler() : 
            base()
        {
            RegistryServiceTask("1", TestActionAsync);
        }

        private ISchemaProcessHandler.ExecuteServiceTaskResult TestActionAsync(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            return ISchemaProcessHandler.ExecuteServiceTaskResult.Result(
                isComplete: true);
        }

        public static ProcessSchemaDto Schema { get; }
            = new ProcessSchemaDto(
                "1",
                [
                    new TokenDto(
                        "1", 
                        new ServiceTaskTokenAction("1", handlerKey: "1")
                        {
                            Transition = ITokenAction.TransitionDto.Target("2"),
                        }
                        ),
                    new TokenDto(
                        "2", 
                        new TimerTokenAction("1", TimeSpan.Zero)
                        {
                            Transition = ITokenAction.TransitionDto.Complete(),
                        })
                ]
                );

        public static ProcessTypeDto ProcessType { get; }
            = new ProcessTypeDto(1, 1);
    }
}
