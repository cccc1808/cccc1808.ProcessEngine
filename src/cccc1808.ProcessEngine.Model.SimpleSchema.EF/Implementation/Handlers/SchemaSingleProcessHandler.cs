using System;
using System.Diagnostics;
using System.Threading;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Service;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Handlers
{
    public class SchemaSingleProcessHandler<TId> 
        : BaseSingleProcessHandler<TId>
    {        
        private readonly ITokenExecutionService<TId> _tokenExecutionService;

        public SchemaSingleProcessHandler(
            IIsolationService isolationService,
            IProcessRepository<TId> repository,
            ITriggerRepository<TId> triggerRepository,
            IProcessSetter processSetter,
            ITokenExecutionService<TId> tokenExecutionService)
            : base(
                  isolationService,
                  repository,
                  triggerRepository,
                  processSetter)
        {
            _tokenExecutionService = tokenExecutionService;
        }

        protected override OptionsDto SingleOptions 
            => Presets<TId>.Preset1_Single;

        protected override async ValueTask StepAsync(
            IProcessContainer<TId> process,
            CancellationToken cancellationToken)
        {
            await _tokenExecutionService.ExecuteTokenAsync(
                process,
                cancellationToken);
        }
    }
}
