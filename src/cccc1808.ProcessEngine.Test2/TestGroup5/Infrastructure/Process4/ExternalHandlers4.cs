using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Service;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process4
{
    internal class ExternalHandlers4
    {
        private readonly ITokenExecutionService<Guid> _tokenExecutionService;

        public ExternalHandlers4(
            ITokenExecutionService<Guid> tokenExecutionService)
        {
            _tokenExecutionService = tokenExecutionService;
        }
        /// <summary>
        /// Хендлер пользовательского ввода 1.
        /// </summary>
        public async Task UIUserInput1Async(
            IProcessContainer<Guid> process,
            int inputValue,
            CancellationToken cancellationToken) 
        {
            var processDatas = process.GetComponent<ISchemaProcessComponent>();
            var signal = BitFlagDto.FromEnum(TestSchemaProcessHandler4.UISignals.UIInput1);

            await _tokenExecutionService.ValidateTokenState(
                process,
                "1",
                "I1",
                signal,
                CancellationToken.None);

            var state = TestSchemaProcessHandler4.GetOrCreateUserInputTokenState(processDatas);
            state.UserInput1 = 1;

            (await _tokenExecutionService.ExecuteActionAsync(process, actionId: "I1", signal, cancellationToken)).ShouldBeTrue();            
        }

        /// <summary>
        /// Хендлер пользовательского ввода 2.
        /// </summary>
        public async Task UIUserInput2Async(
            IProcessContainer<Guid> process,
            int inputValue,
            CancellationToken cancellationToken)
        {
            var processDatas = process.GetComponent<ISchemaProcessComponent>();
            var signal = BitFlagDto.FromEnum(TestSchemaProcessHandler4.UISignals.UIInput2);

            await _tokenExecutionService.ValidateTokenState(
                process,
                "1",
                "I2",
                signal,
                CancellationToken.None);

            var state = TestSchemaProcessHandler4.GetOrCreateUserInputTokenState(processDatas);
            state.UserInput2 = 1;

            (await _tokenExecutionService.ExecuteActionAsync(process, actionId: "I2", signal, cancellationToken)).ShouldBeTrue();
        }

        /// <summary>
        /// Хендлер пользовательского ввода 3.
        /// </summary>
        public async Task UIUserInput3Async(
            IProcessContainer<Guid> process,
            int inputValue,
            CancellationToken cancellationToken)
        {
            var processDatas = process.GetComponent<ISchemaProcessComponent>();
            var signal = BitFlagDto.FromEnum(TestSchemaProcessHandler4.UISignals.UIInput3);

            await _tokenExecutionService.ValidateTokenState(
                process,
                "1",
                "I3",
                signal,
                CancellationToken.None);

            var state = TestSchemaProcessHandler4.GetOrCreateUserInputTokenState(processDatas);
            state.UserInput3 = 1;

            await _tokenExecutionService.ExecuteActionAsync(process, actionId: "I3", signal, cancellationToken);
        }
    }
}
