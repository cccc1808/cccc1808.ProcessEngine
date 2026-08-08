using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services
{
    public class ExternalCounterContext 
        : IExternalCounterContext
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IIsolationService _isolationService;
        private readonly IExternalCounterProvider _externalCounterProvider;

        public ExternalCounterContext(
            ITransactionManager transactionManager,
            IIsolationService isolationService,
            IExternalCounterProvider externalCounterProvider)
        {
            _transactionManager = transactionManager;
            _isolationService = isolationService;
            _externalCounterProvider = externalCounterProvider;
        }

        public async Task CreateCounterAsync(
            string triggerKey,
            int value,
            CancellationToken cancellationToken)
        {
            await _externalCounterProvider.CreateCounterAsync(triggerKey, value, cancellationToken);

            // 2) Регистрируем компенсацию счетчика на случай ошибки.
            if (!_transactionManager.TryGetCurrentTransaction(out var currentTransaction))
            {
                throw new InvalidOperationException("Требуется transaction scope.");
            }

            var param = new ParameterContainer(this, triggerKey, null);

            currentTransaction.AddAfterCommitHandler(
                param,
                commitHandler: static (p, t) => ValueTask.CompletedTask,
                // В случае падения транзакции пробуем удалить счетчик.
                roolbackHandler: static async (p, t) =>
                {
                    var typedParam = (ParameterContainer)p;
                    await typedParam.This._externalCounterProvider.RemoveCounterAsync(typedParam.TriggerKey, t);
                }
                );

            if (_isolationService.TryGetCurrentScopeInfo(out _))
            {
                _isolationService.RegisterManualCompensate(
                    param,
                    // В случае падения транзакции пробуем удалить счетчик.
                    static async (scopeIndex, p, t) =>
                    {
                        var typedParam = (ParameterContainer)p;
                        await typedParam.This._externalCounterProvider.RemoveCounterAsync(typedParam.TriggerKey, t);
                    });
            }
        }

        public async Task<int> TryDecrementCounterAsync(
            string triggerKey,
            string processIdString)
        {
            // 1) Пробуем уменьшить счетчик.
            var counter = await _externalCounterProvider.TryDecrementCounterAsync(
                triggerKey,
                processIdString);

            // 2) Регистрируем компенсацию счетчика на случай ошибки.
            if (!_transactionManager.TryGetCurrentTransaction(out var currentTransaction))
            {
                throw new InvalidOperationException("Требуется transaction scope.");
            }

            var param = new ParameterContainer(this, triggerKey, processIdString);

            currentTransaction.AddAfterCommitHandler(
                param,
                // В случае коммита транзакции удаляем отметку участника счетчика.
                commitHandler: static async (p, t) =>
                {
                    var typedParam = (ParameterContainer)p;
                    await typedParam.This._externalCounterProvider.CommitCounterAsync(typedParam.TriggerKey, typedParam.ProcessIdString);
                },
                // В случае падения транзакции пробуем сбросить.
                roolbackHandler: static async (p, t) =>
                {
                    var typedParam = (ParameterContainer)p;
                    await typedParam.This._externalCounterProvider.CompensateCounterAsync(typedParam.TriggerKey, typedParam.ProcessIdString);
                }
                );

            if (_isolationService.TryGetCurrentScopeInfo(out _))
            {
                _isolationService.RegisterManualCompensate(
                    param,
                    // В случае падения текущего scope пробуем сбросить.
                    static async (scopeIndex, p, t) =>
                    {
                        var typedParam = (ParameterContainer)p;
                        await typedParam.This._externalCounterProvider.CompensateCounterAsync(typedParam.TriggerKey, typedParam.ProcessIdString);
                    });
            }

            return counter;
        }

        private record ParameterContainer(
            ExternalCounterContext This,
            string TriggerKey,
            string ProcessIdString
            );
    }
}
