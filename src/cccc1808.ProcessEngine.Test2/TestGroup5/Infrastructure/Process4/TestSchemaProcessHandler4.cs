using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Handlers;

namespace cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process4
{
    internal class TestSchemaProcessHandler4 : BaseSchemaProcessHandler<Guid>
    {
        public TestSchemaProcessHandler4() :
            base()
        {
            RegistryConditionTaskCheck("I1", UserInput1ExecutedAsync);
            RegistryConditionTaskExecute("I1", UserInput1ExecuteAsync);

            RegistryConditionTaskCheck("I2", UserInput2ExecutedAsync);
            RegistryConditionTaskExecute("I2", UserInput2ExecuteAsync);

            RegistryConditionTaskCheck("I3", UserInput3ExecutedAsync);
            RegistryConditionTaskExecute("I3", UserInput3ExecuteAsync);

            RegistryConditionTaskCheck("R", RAsync);
        }

        private bool UserInput1ExecutedAsync(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            var state = GetOrCreateTokenState(parameters.schemaComponent);            
            return state.UserInput1.HasValue;
        }

        private ISchemaProcessHandler.ExecuteConditionResult UserInput1ExecuteAsync(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            var state = GetOrCreateTokenState(parameters.schemaComponent);
            return ISchemaProcessHandler.ExecuteConditionResult.Result(
                ISchemaProcessHandler.ActivateActionDto.ActivateConditionAction("I2", asyncExecuteOrWaitSignal: false));
        }

        private bool UserInput2ExecutedAsync(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            var state = GetOrCreateTokenState(parameters.schemaComponent);
            return state.UserInput2.HasValue;
        }

        private ISchemaProcessHandler.ExecuteConditionResult UserInput2ExecuteAsync(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            var state = GetOrCreateTokenState(parameters.schemaComponent);
            return ISchemaProcessHandler.ExecuteConditionResult.Result(
                ISchemaProcessHandler.ActivateActionDto.ActivateConditionAction("I3", asyncExecuteOrWaitSignal: false));
        }

        private bool UserInput3ExecutedAsync(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            var state = GetOrCreateTokenState(parameters.schemaComponent);
            return state.UserInput3.HasValue;
        }

        private ISchemaProcessHandler.ExecuteConditionResult UserInput3ExecuteAsync(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            var state = GetOrCreateTokenState(parameters.schemaComponent);

            state.CalculatedResult = state.UserInput1.Value + state.UserInput2.Value + state.UserInput3.Value;

            return new ISchemaProcessHandler.ExecuteConditionResult(
                [new ISchemaProcessHandler.ActivateActionDto("R", AsyncExecuteOrWaitSignal: false)]);
        }

        private bool RAsync(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            return false;
        }

        public static ProcessSchemaDto Schema { get; }
            = new ProcessSchemaDto(
                "1",
                [
                    new TokenDto(
                        "1",
                        new ConditionTokenAction("I1", "I1")
                        {
                            Name = "Пользовательский ввод 1",
                            ActionHandlerKey = "I1",
                            ActivatedOnStart = true,
                        },
                        new ConditionTokenAction("I2", "I2")
                        {
                            Name = "Пользовательский ввод 2",
                            ActionHandlerKey = "I2",
                            ActivatedOnStart = false,
                        },
                        new ConditionTokenAction("I3", "I3")
                        {
                            Name = "Пользовательский ввод 3 и рассчет результата",
                            ActionHandlerKey = "I3",
                            ActivatedOnStart = false,
                        },
                        new ConditionTokenAction("R", "R")
                        {
                            Name = "Чтобы процесс не завершился и валидация не падала",
                            ActivatedOnStart = false,
                            Transition = ITokenAction.TransitionDto.Complete(),
                        }
                        )
                    {
                        Name = "Пользовательский ввод"
                    },
                ]
                );

        public static UserInputTokenState GetOrCreateTokenState(
            ISchemaProcessComponent component)
        {
            var state = (UserInputTokenState?)component.CurrentTokenState 
                ?? new UserInputTokenState() 
                {
                    UserInput1 = null,
                    UserInput2 = null,
                    UserInput3 = null,
                    CalculatedResult = null,
                };
            component.CurrentTokenState = state;

            return state;
        }

        public static ProcessTypeDto ProcessType { get; }
            = new ProcessTypeDto(4, 1);

        public class UserInputTokenState
        {
            public required int? UserInput1 { get; set; }

            public required int? UserInput2 { get; set; }

            public required int? UserInput3 { get; set; }

            public required int? CalculatedResult { get; set; }
        }
    }
}
