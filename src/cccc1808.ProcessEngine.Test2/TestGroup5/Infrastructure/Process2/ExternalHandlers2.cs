using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Service;

namespace cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process2
{
    internal class ExternalHandlers2
    {
        private readonly ITokenExecutionService<Guid> _tokenExecutionService;

        public ExternalHandlers2(
            ITokenExecutionService<Guid> tokenExecutionService)
        {
            _tokenExecutionService = tokenExecutionService;
        }

        public async ValueTask ReponseReceivedAsync(
            IProcessContainer<Guid> process,
            CancellationToken cancellationToken)
        {           
            await _tokenExecutionService.ValidateTokenState(process, "1", "2", cancellationToken);

            var state = TestSchemaProcessHandler2.GetOrCreateTokenState(process.GetComponent<ISchemaProcessComponent>());
            state.IsReceived = true;

            await _tokenExecutionService.ExecuteActionAsync(process, actionId: "2", CancellationToken.None);
        }
    }
}
