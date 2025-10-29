using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Common
{
    public static class TimeoutHelper
    {
        /// <summary>
        /// Выполнить действие с timeout.
        /// По окончанию timeout не выбрасывает Exception.
        /// </summary>
        /// <returns>Timeout или результат.</returns>
        public static async ValueTask<(bool IsTimeout, TResult Result)> ExecuteWithTimeoutAsync<TParam, TResult>(
            TParam param,
            TimeSpan timeout,
            Func<TParam, CancellationToken, ValueTask<TResult>> action,
            CancellationToken cancellationToken
            )
        {
            using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try 
            {
                return (true, await action(param, cancellationTokenSource.Token));
            }
            catch(OperationCanceledException ex)
            {
                if (OperationCancelHelper.IsCancelException(ex, cancellationTokenSource.Token))
                {
                    return (false, default!);
                }

                throw;
            }

            //if (timeout == TimeSpan.Zero)
            //{
            //    return (false, default!);
            //}

            //using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            //try
            //{
            //    var executeValueTask = action(param, cancellationTokenSource.Token);

            //    if (executeValueTask.IsCompleted)
            //    {
            //        return (true, await executeValueTask);
            //    }

            //    var executeTask = executeValueTask.AsTask();
            //    var delayTask = Task.Delay(timeout, cancellationTokenSource.Token);

            //    var completedTask = await Task.WhenAny(
            //        executeTask,
            //        delayTask);

            //    if (completedTask == executeTask)
            //    {
            //        return (true, await executeTask);
            //    }

            //    return (false, default!);
            //}
            //finally 
            //{
            //    cancellationTokenSource.Cancel();
            //}
        }

        /// <summary>
        /// Выполнить действие с timeout.
        /// По окончанию timeout не выбрасывает Exception.
        /// </summary>
        /// <returns>Timeout или результат.</returns>
        public static async ValueTask<bool> ExecuteWithTimeoutAsync<TParam>(
            TParam param,
            TimeSpan timeout,
            Func<TParam, CancellationToken, ValueTask> action,
            CancellationToken cancellationToken
            )
        {
            using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                cancellationTokenSource.CancelAfter(timeout);
                await action(param, cancellationTokenSource.Token);
                return true;
            }
            catch (OperationCanceledException ex)
            {
                if (OperationCancelHelper.IsCancelException(ex, cancellationTokenSource.Token))
                {
                    return false;
                }

                throw;
            }

            //if (timeout == TimeSpan.Zero)
            //{
            //    return (false, default!);
            //}

            //using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            //try
            //{
            //    var executeValueTask = action(param, cancellationTokenSource.Token);

            //    if (executeValueTask.IsCompleted)
            //    {
            //        return (true, await executeValueTask);
            //    }

            //    var executeTask = executeValueTask.AsTask();
            //    var delayTask = Task.Delay(timeout, cancellationTokenSource.Token);

            //    var completedTask = await Task.WhenAny(
            //        executeTask,
            //        delayTask);

            //    if (completedTask == executeTask)
            //    {
            //        return (true, await executeTask);
            //    }

            //    return (false, default!);
            //}
            //finally 
            //{
            //    cancellationTokenSource.Cancel();
            //}
        }
    }
}