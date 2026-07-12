using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.StaticInstance.Abstract.Services;

namespace cccc1808.ProcessEngine.Model.StaticInstance.Implementation.Services
{
    public class StaticInstanceRunner
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IStaticInstanceDeployService _staticInstanceService;

        private readonly OptionsDto _options;

        public StaticInstanceRunner(
            ITransactionManager transactionManager,
            IStaticInstanceDeployService staticInstanceService, 
            OptionsDto options)
        {
            _transactionManager = transactionManager;
            _staticInstanceService = staticInstanceService;
            _options = options;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            if (_options.Validate)
            {
                _staticInstanceService.Validate();
            }

            while(true)
            {
                await using (var transaction = await _transactionManager.StartTransactionAsync(cancellationToken))
                {
                    var result = await _staticInstanceService.TryExecuteAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    if (result)
                    {
                        break;
                    }
                }
                
                await Task.Delay(_options.RetryLockTimeout, cancellationToken);
            }
        }

        public class OptionsDto
        {
            public bool Validate { get; set; }
                = true;

            public TimeSpan RetryLockTimeout { get; set; }
                = TimeSpan.FromSeconds(3);
        }
    }
}
