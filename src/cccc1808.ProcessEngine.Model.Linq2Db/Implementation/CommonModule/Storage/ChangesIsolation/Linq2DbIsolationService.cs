using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.CommonModule.Storage.ChangesIsolation
{
    public class Linq2DbIsolationService
        : IIsolationService
    {
        private static AsyncLocal<ICompensateService.ICompensateScope?> CurrentScope { get; }
            = new AsyncLocal<ICompensateService.ICompensateScope?>();

        private readonly ITransactionManager _transactionManager;
        private readonly INoIsolationCompensateService _noIsolationCompensateService;
        private readonly ISavepointCompensateService _savepointCompensateService;
        private bool _transactionRequired;

        public bool InScope
            => CurrentScope.Value is not null;

        public Linq2DbIsolationService(
            ITransactionManager transactionManager,
            INoIsolationCompensateService noIsolationCompensateService,
            ISavepointCompensateService savepointCompensateService,
            bool transactionRequired = true)
        {
            _transactionManager = transactionManager;
            _noIsolationCompensateService = noIsolationCompensateService;
            _savepointCompensateService = savepointCompensateService;
            _transactionRequired = transactionRequired;
        }

        public async ValueTask ExecuteAsync<TParam>(
            IIsolationService.IsolationMode isolationMode,
            TParam param,
            Func<TParam, CancellationToken, ValueTask> action,
            Func<TParam, Exception, CancellationToken, ValueTask> exceptionHandler,
            Func<TParam, Exception, CancellationToken, ValueTask>? criticalExceptionHandler,
            CancellationToken cancellationToken)
        {
            if (_transactionRequired)
            {
                if (!_transactionManager.TryGetCurrentTransaction(out _))
                {
                    throw new InvalidOperationException("TransactionRequired.");
                }
            }

            switch (isolationMode)
            {
                case IIsolationService.IsolationMode.No:
                    {
                        var noIsolationScope = await _noIsolationCompensateService.StartScopeAsync(cancellationToken);
                        CurrentScope.Value = noIsolationScope;

                        try
                        {
                            try
                            {
                                await action(param, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                if (OperationCancelHelper.IsCancelException(ex, cancellationToken))
                                {
                                    throw;
                                }

                                try
                                {
                                    await exceptionHandler(param, ex, cancellationToken);
                                }
                                catch (Exception ex2)
                                {
                                    if (OperationCancelHelper.IsCancelException(ex2, cancellationToken))
                                    {
                                        throw;
                                    }

                                    var aggregateException = new AggregateException(ex, ex2);
                                    if (criticalExceptionHandler == null)
                                    {
                                        throw aggregateException;
                                    }

                                    await criticalExceptionHandler(param, aggregateException, cancellationToken);
                                }
                            }
                        }
                        finally
                        {
                            await noIsolationScope.DisposeAsync();
                            CurrentScope.Value = null;
                        }

                        break;
                    }

                case IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker:
                    {
                        var savepointScope = await _savepointCompensateService.StartScopeAsync(cancellationToken);
                        CurrentScope.Value = savepointScope;

                        try
                        {
                            try
                            {
                                await action(param, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                if (OperationCancelHelper.IsCancelException(ex, cancellationToken))
                                {
                                    throw;
                                }

                                await savepointScope.CompensateAsync(cancellationToken);

                                try
                                {
                                    await exceptionHandler(param, ex, cancellationToken);
                                }
                                catch (Exception ex2)
                                {
                                    if (OperationCancelHelper.IsCancelException(ex2, cancellationToken))
                                    {
                                        throw;
                                    }

                                    await savepointScope.CompensateAsync(cancellationToken);

                                    var aggregateException = new AggregateException(ex, ex2);
                                    if (criticalExceptionHandler == null)
                                    {
                                        throw aggregateException;
                                    }

                                    await criticalExceptionHandler(param, aggregateException, cancellationToken);
                                }
                            }

                            await savepointScope.CommitAsync(cancellationToken);
                        }
                        finally
                        {
                            await savepointScope.DisposeAsync();
                            CurrentScope.Value = null;
                        }

                        break;
                    }

                case IIsolationService.IsolationMode.ChangeTrackerSnapshot:
                    {
                        throw new NotSupportedException("IIsolationService.IsolationMode.ChangeTrackerSnapshot");
                    }

                case IIsolationService.IsolationMode.ClearChangeTracker:
                    {
                        throw new NotSupportedException("IIsolationService.IsolationMode.ClearChangeTracker");
                    }

                default: throw new NotImplementedException("[Bug].");
            }
        }

        public void RegisterManualCompensate(
            Func<CancellationToken, ValueTask> compensateHandler)
        {
            if (CurrentScope.Value is null)
            {
                throw new InvalidOperationException("Для регистрации хендлера должен быть создан scope изоляции.");
            }

            CurrentScope.Value.RegisterManualCompensateHandler(
                compensateHandler);
        }
    }
}
