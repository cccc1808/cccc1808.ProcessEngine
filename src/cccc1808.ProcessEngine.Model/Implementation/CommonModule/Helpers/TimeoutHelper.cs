using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers
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
            using var wait = new SemaphoreSlim(1);
            wait.Wait();

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

        /// <summary>
        /// Ждать завершения задачи или timeout или отмены.
        /// </summary>
        /// <returns>True - задача завершена, False - timeout.</returns>
        public static async ValueTask<bool> WaitTaskAsync(
            Task waitTask,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            if (timeout.HasValue)
            {
                var timeoutTask = Task.Delay(timeout.Value, cancellationToken);
                var completedTask = await Task.WhenAny(
                    waitTask,
                    timeoutTask);

                return completedTask != timeoutTask;
            }
            else 
            {
                using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var taskCompletionSource = new TaskCompletionSource(
                    creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationTokenSource.Token.Register(
                    static (s, t) =>
                    {
                        var typedState = (TaskCompletionSource)s!;
                        typedState.TrySetCanceled(t);
                    },
                    taskCompletionSource);

                await Task.WhenAny(
                    waitTask,
                    taskCompletionSource.Task);

                return true;
            }
        }
    }
}