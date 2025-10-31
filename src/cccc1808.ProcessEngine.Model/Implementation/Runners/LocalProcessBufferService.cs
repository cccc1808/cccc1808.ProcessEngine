using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Services.Runners;
using cccc1808.ProcessEngine.Model.Common;

namespace cccc1808.ProcessEngine.Model.Implementation.Runners
{
    /// <summary>
    /// Приоритетный буфер с примерным размером.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class LocalProcessBufferService<TId>
        : ILocalProcessBufferService<TId>
    {
        private readonly Channel<ProcessInstanceInfoDto<TId>> _channel;
        private readonly int _sizeLimit;
        private int _size;

        private readonly ConcurrentDictionary<Guid, Action<ILocalProcessBufferService<TId>>> _emptyHandler
            = new ConcurrentDictionary<Guid, Action<ILocalProcessBufferService<TId>>>();

        public int FreeSpace 
            => GetFreeSpace(_size);

        public LocalProcessBufferService(int sizeLimit) 
        {
            _sizeLimit = sizeLimit;
            _channel = Channel.CreateUnboundedPrioritized(
                new UnboundedPrioritizedChannelOptions<ProcessInstanceInfoDto<TId>>() 
                {
                    SingleReader = true,
                    SingleWriter = true,
                    AllowSynchronousContinuations = false,
                    Comparer = new ProcessInstanceInfoDto<TId>.PriorityComparer()
                });
        }

        public async ValueTask<IReadOnlyList<ProcessInstanceInfoDto<TId>>> ConsumeBatchAsync(
            int limit, 
            TimeSpan timeout, 
            CancellationToken cancellationToken)
        {
            var buffer = new List<ProcessInstanceInfoDto<TId>>(limit);

            await TimeoutHelper.ExecuteWithTimeoutAsync(
                (limit, buffer, This: this, cancellationToken),
                timeout,
                static async (p) => 
                {
                    while (p.buffer.Count < p.limit)
                    {
                        if (p.This._channel.Reader.TryRead(out var item))
                        {
                            Interlocked.Decrement(ref p.This._size);
                            p.buffer.Add(item);
                        }
                        else
                        {
                            if (p.buffer.Count != 0)
                            {
                                foreach (var elem in p.This._emptyHandler.Values)
                                {
                                    elem(p.This);
                                }
                            }

                            await p.This._channel.Reader.WaitToReadAsync(p.cancellationToken);
                        }
                    }
                }
                );

            return buffer;
        }

        public async ValueTask<IReadOnlyList<ProcessInstanceInfoDto<TId>>> ConsumeBatch2Async(
            int limit,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var buffer = new List<ProcessInstanceInfoDto<TId>>(limit);


            while (buffer.Count < limit)
            {
                if (_channel.Reader.TryRead(out var item))
                {
                    Interlocked.Decrement(ref _size);
                    buffer.Add(item);

                    // Таймер запускается только после считывания первого элемента
                    await TimeoutHelper.ExecuteWithTimeoutAsync(
                        (limit, buffer, This: this, cancellationToken),
                        timeout,
                        static async (p) =>
                        {
                            while (p.buffer.Count < p.limit)
                            {
                                if (p.This._channel.Reader.TryRead(out var item))
                                {
                                    Interlocked.Decrement(ref p.This._size);
                                    p.buffer.Add(item);
                                }
                                else
                                {
                                    foreach (var elem in p.This._emptyHandler.Values)
                                    {
                                        elem(p.This);
                                    }

                                    await p.This._channel.Reader.WaitToReadAsync(p.cancellationToken);
                                }
                            }
                        });
                }
                else
                {
                    await _channel.Reader.WaitToReadAsync(cancellationToken);
                }
            }

            return buffer;
        }

        public (int FreeSpace, Queue<ProcessInstanceInfoDto<TId>> ids) TryProduce(
            Queue<ProcessInstanceInfoDto<TId>> ids)
        {
            int currentSize = _size;

            if (GetFreeSpace(currentSize) == 0)
            {
                return (0, ids);
            }
            
            while(ids.TryDequeue(out var elem))
            {
                _channel.Writer.TryWrite(elem);
                currentSize = Interlocked.Increment(ref _size);

                if (GetFreeSpace(currentSize) == 0)
                {
                    return (0, ids);
                }
            }

            return (currentSize, ids);
        }

        private int GetFreeSpace(int size)
            => Math.Max(_sizeLimit - size, 0);

        public IDisposable AddEmptyHandler(
            Action<ILocalProcessBufferService<TId>> handler)
        {
            var id = Guid.NewGuid();
            _emptyHandler.TryAdd(id, handler);

            return new DisposeHandler(
                () => _emptyHandler.TryRemove(id, out _));
        }

        private readonly record struct DisposeHandler : IDisposable
        {
            private readonly Action _action;

            public DisposeHandler(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                _action();
            }
        }
    }
}
