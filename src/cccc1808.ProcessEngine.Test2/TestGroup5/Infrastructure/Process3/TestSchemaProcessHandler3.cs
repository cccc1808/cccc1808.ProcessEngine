using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Handlers;

namespace cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process3
{
    internal class TestSchemaProcessHandler3 : BaseSchemaProcessHandler<Guid>
    {
        private readonly TestRequestReponseStore _testRequestReponseStore;
        private readonly IProcessSetter _processSetter;

        public TestSchemaProcessHandler3(
            TestRequestReponseStore testRequestReponseStore, IProcessSetter processSetter) :
            base()
        {
            _testRequestReponseStore = testRequestReponseStore;

            RegistryServiceTask("SendRequest", SendRequestHandlerAsync);
            RegistryConditionTaskCheck("CheckResponse", CheckReponseReceivedAsync);
            RegistryTimerTask("", WaitResponseTimeoutTimerAsync);
            _processSetter = processSetter;
        }

        private ValueTask<ISchemaProcessHandler.ExecuteServiceTaskResult> SendRequestHandlerAsync(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            var state = GetOrCreateTokenState(parameters);

            state.CorrelationId = Guid.NewGuid().ToString();
            state.IsReceived = false;
            state.TryCount++;

            _testRequestReponseStore.SendRequest(
                state.CorrelationId,
                JsonHelper.Empty);
            
            return ValueTask.FromResult(
                ISchemaProcessHandler.ExecuteServiceTaskResult.Result(
                    isComplete: true,
                    ISchemaProcessHandler.ActivateActionDto.ConditionAction("2", asyncExecuteOrWaitSignal: false),
                    ISchemaProcessHandler.ActivateActionDto.TimerAction("3"))
                );
        }

        private ValueTask<bool> CheckReponseReceivedAsync(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            var state = GetOrCreateTokenState(parameters);

            if (state.CorrelationId is null)
            {
                // Запрос еще не отправлен.
                return ValueTask.FromResult(false);
            }
            if (state.IsReceived)
            {
                // Ответ пришел.
                return ValueTask.FromResult(true);
            }

            state.IsReceived = _testRequestReponseStore.ResponseReceived(state.CorrelationId, out _);
            return ValueTask.FromResult(state.IsReceived);
        }

        private ISchemaProcessHandler.ExecuteTimerResult WaitResponseTimeoutTimerAsync(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            var state = GetOrCreateTokenState(parameters);

            if (state.IsReceived)
            {
                // Ответ пришел. Ничего не делаем.
                return ISchemaProcessHandler.ExecuteTimerResult.Result();
            }

            if (state.TryCount < 3)
            {
                // Посылаем запрос повторно.
                return ISchemaProcessHandler.ExecuteTimerResult.Result(
                    ISchemaProcessHandler.ActivateActionDto.ServiceTask("1"));
            }

            state.TryCount = 0;
            _processSetter.SetError(
                parameters.process,
                new Exception($"Превышено количетсво попыток отправить запрос, ответ не получен {parameters.schemaComponent.CurrentTokenId}."),
                allowRetry: false);

            return ISchemaProcessHandler.ExecuteTimerResult.Result();
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
                        },
                        new TimerTokenAction("3", TimeSpan.FromSeconds(30))
                        {
                            Name = "Timeout ожидания ответа.",
                            ActivatedOnStart = false,
                        }
                        )
                    {
                        Name = "RPC with retry."
                    },
                ]
                );

        private static RpcTokenState GetOrCreateTokenState(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters)
        {
            var state = (RpcTokenState?)parameters.schemaComponent.CurrentTokenState 
                ?? new RpcTokenState() 
                {
                    CorrelationId = null,
                    IsReceived = false,
                    TryCount = 0
                };
            parameters.schemaComponent.CurrentTokenState = state;

            return state;
        }

        public static ProcessTypeDto ProcessType { get; }
            = new ProcessTypeDto(2, 1);

        public class RpcTokenState : SchemaProcessStateTypelessHandler.ITypeContainer
        {
            public string? AssemblyQualifiedName { get; set; }

            public required int TryCount { get; set; }

            public required string? CorrelationId { get; set; }

            public required bool IsReceived { get; set; }            
        }
    }
}
