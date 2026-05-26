using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage.ChangesIsolation
{
    public class EFIsolationService
        : IIsolationService
    {
        private static AsyncLocal<ICompensateService.ICompensateScope?> CurrentScope { get; }
            = new AsyncLocal<ICompensateService.ICompensateScope?>();

        private readonly ITransactionManager _transactionManager;
        private readonly INoIsolationCompensateService _noIsolationCompensateService;
        private readonly ISavepointCompensateService _savepointCompensateService;
        private readonly IChangeTrackerCompensateService _changeTrackerCompensateService;
        private readonly IChangeTrackerSnapshotCompensateService _changeTrackerSnapshotCompensateService;
        private bool _transactionRequired;        

        public bool InScope
            => CurrentScope.Value is not null;

        public EFIsolationService(
            ITransactionManager transactionManager,
            INoIsolationCompensateService noIsolationCompensateService,
            ISavepointCompensateService savepointCompensateService, 
            IChangeTrackerCompensateService changeTrackerCompensateService,
            IChangeTrackerSnapshotCompensateService changeTrackerSnapshotCompensateService,
            bool transactionRequired = true)
        {
            _transactionManager = transactionManager;
            _noIsolationCompensateService = noIsolationCompensateService;
            _savepointCompensateService = savepointCompensateService;
            _changeTrackerCompensateService = changeTrackerCompensateService;
            _changeTrackerSnapshotCompensateService = changeTrackerSnapshotCompensateService;
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

                case IIsolationService.IsolationMode.ClearChangeTracker:
                    {
                        var changeTrackerClearScope = await _changeTrackerCompensateService.StartScopeAsync(cancellationToken);
                        CurrentScope.Value = changeTrackerClearScope;

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

                                await changeTrackerClearScope.CompensateAsync(cancellationToken);

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

                                    await changeTrackerClearScope.CompensateAsync(cancellationToken);

                                    var aggregateException = new AggregateException(ex, ex2);
                                    if (criticalExceptionHandler == null)
                                    {
                                        throw aggregateException;
                                    }

                                    await criticalExceptionHandler(param, aggregateException, cancellationToken);
                                }
                            }

                            await changeTrackerClearScope.CommitAsync(cancellationToken);
                        }
                        finally 
                        {
                            await changeTrackerClearScope.DisposeAsync();
                            CurrentScope.Value = null;
                        }

                        break;
                    }

                case IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker:
                    {
                        var changeTrackerClearScope = await _changeTrackerCompensateService.StartScopeAsync(cancellationToken);
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
                                await changeTrackerClearScope.CompensateAsync(cancellationToken);

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
                                    await changeTrackerClearScope.CompensateAsync(cancellationToken);

                                    var aggregateException = new AggregateException(ex, ex2);
                                    if (criticalExceptionHandler == null)
                                    {
                                        throw aggregateException;
                                    }

                                    await criticalExceptionHandler(param, aggregateException, cancellationToken);
                                }
                            }

                            await savepointScope.CommitAsync(cancellationToken);
                            await changeTrackerClearScope.CommitAsync(cancellationToken);
                        }
                        finally 
                        {
                            await changeTrackerClearScope.DisposeAsync();
                            await savepointScope.DisposeAsync();
                            CurrentScope.Value = null;
                        }

                        break;
                    }

                case IIsolationService.IsolationMode.ChangeTrackerSnapshot:
                    {
                        var changeTrackerSnapshotScope = await _changeTrackerSnapshotCompensateService.StartScopeAsync(cancellationToken);
                        CurrentScope.Value = changeTrackerSnapshotScope;

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

                                await changeTrackerSnapshotScope.CompensateAsync(cancellationToken);

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

                                    await changeTrackerSnapshotScope.CompensateAsync(cancellationToken);

                                    var aggregateException = new AggregateException(ex, ex2);
                                    if (criticalExceptionHandler == null)
                                    {
                                        throw aggregateException;
                                    }

                                    await criticalExceptionHandler(param, aggregateException, cancellationToken);
                                }
                            }

                            await changeTrackerSnapshotScope.CommitAsync(cancellationToken);
                        }
                        finally 
                        {
                            await changeTrackerSnapshotScope.DisposeAsync();
                            CurrentScope.Value = null;
                        }

                        break;
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
