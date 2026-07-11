using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Services;

namespace cccc1808.ProcessEngine.Model.StaticInstance.EF.Implementation.Services
{
    public class EFStaticInstanceRunner
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IEFDbContext _dbContext;
        private readonly IStaticInstanceDeployService _staticInstanceService;

        private readonly OptionsDto _options;

        public EFStaticInstanceRunner(
            ITransactionManager transactionManager,
            IEFDbContext dbContext,
            IStaticInstanceDeployService staticInstanceService, 
            OptionsDto options)
        {
            _transactionManager = transactionManager;
            _dbContext = dbContext;
            _staticInstanceService = staticInstanceService;
            _options = options;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            while(true)
            {
                await using (var transaction = await _transactionManager.StartTransactionAsync(cancellationToken))
                {
                    var result = await _staticInstanceService.TryExecuteAsync(cancellationToken);

                    await _dbContext.SaveChangesAsync(cancellationToken);
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
            public TimeSpan RetryLockTimeout { get; set; }
                = TimeSpan.FromSeconds(3);
        }
    }
}
