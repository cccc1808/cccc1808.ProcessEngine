using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Limiter;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Limiter
{
    public class ProcessCountLimiter
        : IExecuteLimiter
    {
        private readonly int _limit;
        private readonly object _locker = new object();
        private readonly Queue<TaskCompletionSource> _waitBuffer;

        private int _count;

        public ProcessCountLimiter(
            int limit)
        {
            _limit = limit;
            _waitBuffer = new Queue<TaskCompletionSource>();
            _count = 0;
        }

        public void Start(int count) 
        {
            lock(_locker)
            {
                _count = _count + count;
            }            
        }

        public void Stop(int count)
        {
            lock (_locker)
            {
                _count = _count - count;

                if (_count < _limit)
                {
                    while(_waitBuffer.TryDequeue(out var elem))
                    {
                        elem.TrySetResult();
                    }
                }
            }
        }

        public async ValueTask WaitNextAsync(CancellationToken cancellationToken)
        {
            TaskCompletionSource wait;
            lock (_locker)
            {
                if (_count < _limit)
                {
                    return;
                }

                wait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waitBuffer.Enqueue(wait);
            }

            using (cancellationToken.Register(() => {
                // this callback will be executed when token is cancelled
                wait.TrySetCanceled();
            }))
            {
                // ...
                await wait.Task;
            }
        }
    }
}
