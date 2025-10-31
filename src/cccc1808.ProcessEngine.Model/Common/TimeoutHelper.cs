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
            cancellationTokenSource.CancelAfter(timeout);
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
            cancellationTokenSource.CancelAfter(timeout);
            try
            {
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
        }

        public static async ValueTask<bool> ExecuteWithTimeoutAsync<TParam>(
            TParam param,
            TimeSpan timeout,
            Func<TParam, ValueTask> action)
        {
            using var wait = new SemaphoreSlim(1, 1);

            Task executeTask;
            {
                var executeValueTask = action(param);

                if (executeValueTask.IsCompleted)
                {
                    await executeValueTask;
                    return true;
                }

                executeTask = executeValueTask.AsTask();
            }

            var delayTask = wait.WaitAsync(timeout);

            var completedTask = await Task.WhenAny(
                executeTask,
                delayTask);

            if (completedTask == executeTask)
            {
                await executeTask;
                return true;
            }

            return false;
        }
    }
}