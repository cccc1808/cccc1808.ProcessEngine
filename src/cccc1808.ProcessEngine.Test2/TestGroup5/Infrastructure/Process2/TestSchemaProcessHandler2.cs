using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Handlers;
using cccc1808.ProcessEngine.Model.SimpleSchema.Implementation.Handlers;

namespace cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process2
{
    internal class TestSchemaProcessHandler2 : BaseSchemaProcessHandler<Guid>
    {
        public static ProcessSchemaDto Schema { get; }
            = new ProcessSchemaDto(
                "1",
                [
                    new TokenDto(
                        "1",
                        new ServiceTaskTokenAction("1", handlerKey: "SendRequest")
                        {
                            Name = "Отправляем запрос.",
                            ActivatedOnStart = true,
                        },
                        new ConditionTokenAction("2", checkHandlerKey: "CheckResponse")
                        {
                            Name = "Ждем ответ.",
                            ActivatedOnStart = false,
                            Transition = ITokenAction.TransitionDto.Complete(),
                        }
                        )
                    {
                        Name = "RPC"
                    },
                ]
                );

        public static ProcessTypeDto ProcessType { get; }
            = new ProcessTypeDto(2, 1);

        public TestSchemaProcessHandler2() :
            base()
        {
            RegistryServiceTask("SendRequest", SendRequestHandler);
            RegistryConditionTaskCheck("CheckResponse", CheckReponseReceived);
        }

        #region handlers

        private ISchemaProcessHandler.ExecuteServiceTaskResult SendRequestHandler(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters)
        {
            var typedTokenState = new RpcTokenState()
            {
                CorrelationId = Guid.NewGuid().ToString()
            };
            parameters.schemaComponent.CurrentTokenState = typedTokenState;

            // Send request

            return ISchemaProcessHandler.ExecuteServiceTaskResult.Result(
                isComplete: true,
                ISchemaProcessHandler.ActivateActionDto.ConditionAction("2", asyncExecuteOrWaitSignal: false)
                );
        }

        private bool CheckReponseReceived(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters)
        {
            var state = GetOrCreateTokenState(parameters.schemaComponent);
            return state.IsReceived;
        }

        #endregion

        #region state

        public static RpcTokenState GetOrCreateTokenState(
            ISchemaProcessComponent component)
        {
            var state = (RpcTokenState?)component.CurrentTokenState
                ?? new RpcTokenState()
                {
                    CorrelationId = null,
                    IsReceived = false,
                };
            component.CurrentTokenState = state;

            return state;
        }

        public class RpcTokenState : SchemaProcessStateTypelessHandler.ITypeContainer
        {
            public string? AssemblyQualifiedName { get; set; }

            public string? CorrelationId { get; set; }

            public bool IsReceived { get; set; }
        }

        #endregion
    }
}
