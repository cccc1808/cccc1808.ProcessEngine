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
        private readonly ITransactionManager _transactionManager;
        private readonly ISavepointCompensateService _savepointCompensateService;
        private readonly IManualCompensateService _manualCompensateService;
        private bool _transactionRequired;

        public Linq2DbIsolationService(
            ITransactionManager transactionManager,
            ISavepointCompensateService savepointCompensateService,
            IManualCompensateService manualCompensateService,
            bool transactionRequired = true)
        {
            _transactionManager = transactionManager;
            _savepointCompensateService = savepointCompensateService;
            _manualCompensateService = manualCompensateService;
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

                        break;
                    }

                case IIsolationService.IsolationMode.ClearChangeTracker:
                    {                       
                        break;
                    }

                case IIsolationService.IsolationMode.Manual:
                    {
                        await using (var manual = await _manualCompensateService.StartScopeAsync(cancellationToken))
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

                                await manual.CompensateAsync(cancellationToken);

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

                                    await manual.CompensateAsync(cancellationToken);

                                    var aggregateException = new AggregateException(ex, ex2);
                                    if (criticalExceptionHandler == null)
                                    {
                                        throw aggregateException;
                                    }

                                    await criticalExceptionHandler(param, aggregateException, cancellationToken);
                                }
                            }

                            await manual.CommitAsync(cancellationToken);
                        }

                        break;
                    }

                case IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker:
                    {
                        await using (var savepoint = await _savepointCompensateService.StartScopeAsync(cancellationToken))
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

                                await savepoint.CompensateAsync(cancellationToken);

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

                                    await savepoint.CompensateAsync(cancellationToken);

                                    var aggregateException = new AggregateException(ex, ex2);
                                    if (criticalExceptionHandler == null)
                                    {
                                        throw aggregateException;
                                    }

                                    await criticalExceptionHandler(param, aggregateException, cancellationToken);
                                }
                            }

                            await savepoint.CommitAsync(cancellationToken);
                        }

                        break;
                    }

                case IIsolationService.IsolationMode.ChangeTrackerSnapshot:
                    {
                        throw new NotSupportedException("IIsolationService.IsolationMode.ChangeTrackerSnapshot");
                    }

                case IIsolationService.IsolationMode.ChangeTrackerSnapshotAndManual:
                    {
                        throw new NotSupportedException("IIsolationService.IsolationMode.ChangeTrackerSnapshotAndManual");
                    }

                default: throw new NotImplementedException("[Bug].");
            }
        }
    }
}
