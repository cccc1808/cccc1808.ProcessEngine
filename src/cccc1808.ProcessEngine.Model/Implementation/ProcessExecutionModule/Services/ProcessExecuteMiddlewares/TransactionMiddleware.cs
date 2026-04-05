using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares
{
    public class TransactionMiddleware<TId>
        : IProcessHandlerMiddleware<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Func<IServiceProvider, IProcessHandlerMiddleware<TId>> _next;
        private readonly ITransactionManager _transactionManager;

        public TransactionMiddleware(
            IServiceProvider serviceProvider,
            Func<IServiceProvider, IProcessHandlerMiddleware<TId>> next,
            ITransactionManager transactionManager)
        {
            _serviceProvider = serviceProvider;
            _next = next;
            _transactionManager = transactionManager;
        }

        public async ValueTask HandleRangeAsync(
            IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>> ids,
            CancellationToken cancellationToken)
        {
            await using (var transaction = await _transactionManager.StartTransactionAsync(cancellationToken))
            {
                var handler = _next(_serviceProvider);
                await handler.HandleRangeAsync(ids, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
        }
    }
}
