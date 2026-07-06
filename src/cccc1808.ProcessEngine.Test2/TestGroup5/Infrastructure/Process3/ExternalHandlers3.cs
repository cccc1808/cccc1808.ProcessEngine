using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Service;

namespace cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process3
{
    internal class ExternalHandlers3
    {
        private readonly ITokenExecutionService<Guid> _tokenExecutionService;

        public ExternalHandlers3(
            ITokenExecutionService<Guid> tokenExecutionService)
        {
            _tokenExecutionService = tokenExecutionService;
        }

        public async ValueTask ReponseReceivedAsync(
            IProcessContainer<Guid> process,
            CancellationToken cancellationToken)
        {           
            await _tokenExecutionService.ValidateTokenState(process, "1", "2", signalCode: null, cancellationToken);

            var state = TestSchemaProcessHandler3.GetOrCreateTokenState(process.GetComponent<ISchemaProcessComponent>());
            state.IsReceived = true;

            await _tokenExecutionService.ExecuteActionAsync(process, actionId: "2", null, CancellationToken.None);
        }
    }
}
