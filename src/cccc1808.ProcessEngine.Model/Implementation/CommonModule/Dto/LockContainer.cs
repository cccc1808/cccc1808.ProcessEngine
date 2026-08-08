using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using Nito.AsyncEx;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto
{
    public static class LockContainer 
    {
        public static async ValueTask<T> DoubleCheckPatternAsync<T, TParameter>(
            this LockContainer<T> container,
            TParameter parameter,
            Func<TParameter, T, CancellationToken, ValueTask<bool>> checkAction,
            Func<TParameter, CancellationToken, ValueTask<T>> valueFactory,
            CancellationToken cancellationToken)
        {
            var checkResult = await container.Read(
                (parameter, checkAction),
                static async (p, d, t) => (Data: d, IsSuccess: await p.checkAction(p.parameter, d, t)),
                cancellationToken
                );
            if (checkResult.IsSuccess)
            {
                return checkResult.Data;
            }

            return await container.Write(
                (parameter, checkAction, valueFactory),
                static async (p, d, t) => 
                {
                    var checkResult = await p.checkAction(p.parameter, d, t);
                    if (!checkResult)
                    {
                        d = await p.valueFactory(p.parameter, t);
                    }

                    return d;
                },
                cancellationToken
                );
        }

        public static async ValueTask<T> DoubleCheckPatternAsync<T, TParameter>(
            this LockContainer<T> container,
            TParameter parameter,
            Func<TParameter, T, bool> checkAction,
            Func<TParameter, CancellationToken, ValueTask<T>> valueFactory,
            CancellationToken cancellationToken)
        {
            var checkResult = await container.Read(
                (parameter, checkAction),
                static (p, d, _) => ValueTask.FromResult(
                    (Data: d, IsSuccess: p.checkAction(p.parameter, d))),
                cancellationToken
                );
            if (checkResult.IsSuccess)
            {
                return checkResult.Data;
            }

            return await container.Write(
                (parameter, checkAction, valueFactory),
                static async (p, d, t) =>
                {
                    var checkResult = p.checkAction(p.parameter, d);
                    if (!checkResult)
                    {
                        d = await p.valueFactory(p.parameter, t);
                    }

                    return d;
                },
                cancellationToken
                );
        }

        public static async ValueTask<T> DoubleCheckPatternAsync<T, TParameter>(
            this LockContainer<T> container,
            TParameter parameter,
            Func<TParameter, T, bool> checkAction,
            Func<TParameter, T, CancellationToken, ValueTask<T>> valueFactory,
            CancellationToken cancellationToken)
        {
            var checkResult = await container.Read(
                (parameter, checkAction),
                static (p, d, _) => ValueTask.FromResult(
                    (Data: d, IsSuccess: p.checkAction(p.parameter, d))),
                cancellationToken
                );
            if (checkResult.IsSuccess)
            {
                return checkResult.Data;
            }

            return await container.Write(
                (parameter, checkAction, valueFactory),
                static async (p, d, t) =>
                {
                    var checkResult = p.checkAction(p.parameter, d);
                    if (!checkResult)
                    {
                        d = await p.valueFactory(p.parameter, d, t);
                    }

                    return d;
                },
                cancellationToken
                );
        }

        public static async ValueTask<T> DoubleCheckPatternAsync<T, TParameter>(
            this LockContainer<T> container,
            TParameter parameter,
            Func<TParameter, T, bool> checkAction,
            Func<TParameter, T, T> valueFactory,
            CancellationToken cancellationToken)
        {
            var checkResult = await container.Read(
                (parameter, checkAction),
                static (p, d, _) => ValueTask.FromResult(
                    (Data: d, IsSuccess: p.checkAction(p.parameter, d))),
                cancellationToken
                );
            if (checkResult.IsSuccess)
            {
                return checkResult.Data;
            }

            return await container.Write(
                (parameter, checkAction, valueFactory),
                static async (p, d, t) =>
                {
                    var checkResult = p.checkAction(p.parameter, d);
                    if (!checkResult)
                    {
                        d = p.valueFactory(p.parameter, d);
                    }

                    return d;
                },
                cancellationToken
                );
        }
    }

    public class LockContainer<T> : IDisposable
    {
        private readonly AsyncReaderWriterLock _lock;

        private bool IsDisposed { get; set; }

        private T Data { get; set; } = default!;

        public LockContainer()
        {
            _lock = new AsyncReaderWriterLock();
        }

        public LockContainer(T data)
        {
            _lock = new AsyncReaderWriterLock();
            Data = data;
        }

        public async ValueTask<TResult> Read<TParameter, TResult>(
            TParameter parameter,
            Func<TParameter, T, CancellationToken, ValueTask<TResult>> action,
            CancellationToken cancellationToken)
        {
            using var _ = await _lock.ReaderLockAsync(cancellationToken);

            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            return await action(parameter, Data, cancellationToken);
        }

        public async ValueTask<T> Write<TParameter>(
            TParameter parameter,
            Func<TParameter, T, CancellationToken, ValueTask<T>> action,
            CancellationToken cancellationToken)
        {
            using var _ = await _lock.WriterLockAsync(cancellationToken);

            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            var result = await action(parameter, Data, cancellationToken);
            Data = result;

            return result;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }        
    }
}
