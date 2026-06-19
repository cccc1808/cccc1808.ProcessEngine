using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Handlers;

using static cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers.ISchemaProcessHandler;

namespace cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process2
{
    internal class TestSchemaProcessHandler2 : BaseSchemaProcessHandler<Guid>
    {
        private readonly TestRequestReponseStore _testRequestReponseStore;

        public TestSchemaProcessHandler2(
            TestRequestReponseStore testRequestReponseStore) :
            base()
        {
            _testRequestReponseStore = testRequestReponseStore;

            RegistryServiceTask("SendRequest", SendRequestHandlerAsync);
            RegistryConditionTaskCheck("CheckResponse", CheckReponseReceivedAsync);
        }

        private ISchemaProcessHandler.ExecuteServiceTaskResult SendRequestHandlerAsync(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            var typedTokenState = new RpcTokenState()
            {
                CorrelationId = Guid.NewGuid().ToString()
            };
            parameters.schemaComponent.CurrentTokenState = typedTokenState;

            _testRequestReponseStore.SendRequest(
                typedTokenState.CorrelationId,
                JsonHelper.Empty);

            return ISchemaProcessHandler.ExecuteServiceTaskResult.Result(
                isComplete: true,
                ActivateActionDto.ActivateConditionAction("2", asyncExecuteOrWaitSignal: false)
                );
        }

        private bool CheckReponseReceivedAsync(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            var typedTokenState = (RpcTokenState?)parameters.schemaComponent.CurrentTokenState;

            // Запрос еще не отправлен.
            if (typedTokenState is null)
            {
                return false;
            }

            var received = _testRequestReponseStore.ResponseReceived(typedTokenState.CorrelationId, out _);
            return received;
        }

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

        public class RpcTokenState
        {
            public required string CorrelationId { get; set; }
        }
    }
}
