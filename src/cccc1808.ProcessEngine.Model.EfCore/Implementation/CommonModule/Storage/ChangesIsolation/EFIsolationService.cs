using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage.ChangesIsolation
{
    public class EFIsolationService<TId>
        : IIsolationService<TId>
    {
        private readonly ITransactionManager _transactionManager;
        private readonly ISavepointCompensateService<TId> _savepointCompensateService;
        private readonly IChangeTrackerCompensateService<TId> _changeTrackerCompensateService;
        private readonly IChangeTrackerSnapshotCompensateService<TId> _changeTrackerSnapshotCompensateService;
        private readonly IManualCompensateService<TId> _manualCompensateService;
        private bool _transactionRequired;

        public EFIsolationService(
            ITransactionManager transactionManager,
            ISavepointCompensateService<TId> savepointCompensateService, 
            IChangeTrackerCompensateService<TId> changeTrackerCompensateService,
            IChangeTrackerSnapshotCompensateService<TId> changeTrackerSnapshotCompensateService,
            IManualCompensateService<TId> manualCompensateService,
            bool transactionRequired = true)
        {
            _transactionManager = transactionManager;
            _savepointCompensateService = savepointCompensateService;
            _changeTrackerCompensateService = changeTrackerCompensateService;
            _changeTrackerSnapshotCompensateService = changeTrackerSnapshotCompensateService;
            _manualCompensateService = manualCompensateService;
            _transactionRequired = transactionRequired;
        }

        public async ValueTask ExecuteAsync<TParam>(
            IDictionary<TId, IProcessContainer<TId>> processes,
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
                        await using (var changeTrackerClear = await _changeTrackerCompensateService.StartScopeAsync(processes, cancellationToken))
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

                                await changeTrackerClear.CompensateAsync(cancellationToken);

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

                                    await changeTrackerClear.CompensateAsync(cancellationToken);

                                    var aggregateException = new AggregateException(ex, ex2);
                                    if (criticalExceptionHandler == null)
                                    {
                                        throw aggregateException;
                                    }

                                    await criticalExceptionHandler(param, aggregateException, cancellationToken);
                                }
                            }

                            await changeTrackerClear.CommitAsync(cancellationToken);
                        }

                        break;
                    }

                case IIsolationService.IsolationMode.Manual:
                    {
                        await using (var manual = await _manualCompensateService.StartScopeAsync(processes, cancellationToken))
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
                        await using (var changeTrackerClear = await _changeTrackerCompensateService.StartScopeAsync(processes, cancellationToken))
                        await using (var savepoint = await _savepointCompensateService.StartScopeAsync(processes, cancellationToken))
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
                                await changeTrackerClear.CompensateAsync(cancellationToken);

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
                                    await changeTrackerClear.CompensateAsync(cancellationToken);

                                    var aggregateException = new AggregateException(ex, ex2);
                                    if (criticalExceptionHandler == null)
                                    {
                                        throw aggregateException;
                                    }

                                    await criticalExceptionHandler(param, aggregateException, cancellationToken);
                                }
                            }

                            await savepoint.CommitAsync(cancellationToken);
                            await changeTrackerClear.CommitAsync(cancellationToken);
                        }

                        break;
                    }

                case IIsolationService.IsolationMode.ChangeTrackerSnapshot:
                    {
                        await using (var changeTrackerSnapshot = await _changeTrackerSnapshotCompensateService.StartScopeAsync(processes, cancellationToken))
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

                                await changeTrackerSnapshot.CompensateAsync(cancellationToken);

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

                                    await changeTrackerSnapshot.CompensateAsync(cancellationToken);

                                    var aggregateException = new AggregateException(ex, ex2);
                                    if (criticalExceptionHandler == null)
                                    {
                                        throw aggregateException;
                                    }

                                    await criticalExceptionHandler(param, aggregateException, cancellationToken);
                                }
                            }

                            await changeTrackerSnapshot.CommitAsync(cancellationToken);
                        }

                        break;
                    }

                case IIsolationService.IsolationMode.ChangeTrackerSnapshotAndManual:
                    {
                        await using (var manualCompensate = await _manualCompensateService.StartScopeAsync(processes, cancellationToken))
                        await using (var changeTrackerSnapshot = await _changeTrackerSnapshotCompensateService.StartScopeAsync(processes, cancellationToken))
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

                                await manualCompensate.CompensateAsync(cancellationToken);
                                await changeTrackerSnapshot.CompensateAsync(cancellationToken);

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

                                    await manualCompensate.CompensateAsync(cancellationToken);
                                    await changeTrackerSnapshot.CompensateAsync(cancellationToken);

                                    var aggregateException = new AggregateException(ex, ex2);
                                    if (criticalExceptionHandler == null)
                                    {
                                        throw aggregateException;
                                    }

                                    await criticalExceptionHandler(param, aggregateException, cancellationToken);
                                }
                            }

                            await manualCompensate.CommitAsync(cancellationToken);
                            await changeTrackerSnapshot.CommitAsync(cancellationToken);
                        }

                        break;
                    }

                default: throw new NotImplementedException("[Bug].");
            }
        }
    }
}
