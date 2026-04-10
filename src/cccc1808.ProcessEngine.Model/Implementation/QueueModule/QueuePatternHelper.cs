using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;

namespace cccc1808.ProcessEngine.Model.Implementation.QueueModule
{
    public static class QueuePatternHelper
    {
        public static async Task<IQueueConsumer> ConnectOrReconnectConsumerAsync(
            IQueueProviderFactory queueProviderFactory,
            TimeSpan exceptionDelay,
            IQueueConsumer? consumer,
            string queueName,
            bool oneExecute,
            Action<Exception> log,
            CancellationToken cancelationToken)
        {
            if (consumer == null)
            {
                try
                {
                    consumer = await queueProviderFactory.GetConsumerAsync(queueName, cancelationToken);
                    return consumer;
                }
                catch (Exception ex)
                {
                    if (OperationCancelHelper.IsCancelException(ex, cancelationToken))
                    {
                        throw;
                    }

                    log(ex);

                    if (oneExecute)
                    {
                        throw;
                    }
                }
            }

            while (true)
            {
                await Task.Delay(exceptionDelay, cancelationToken);

                try
                {
                    await queueProviderFactory.DisconnectConsumerAsync(queueName, cancelationToken);
                    consumer = null;
                }
                catch (Exception ex)
                {
                    if (OperationCancelHelper.IsCancelException(ex, cancelationToken))
                    {
                        throw;
                    }

                    log(ex);

                    continue;
                }

                try
                {
                    consumer = await queueProviderFactory.GetConsumerAsync(queueName, cancelationToken);
                    return consumer;
                }
                catch (Exception ex)
                {
                    if (OperationCancelHelper.IsCancelException(ex, cancelationToken))
                    {
                        throw;
                    }

                    log(ex);

                    continue;
                }
            }
        }
    }
}
