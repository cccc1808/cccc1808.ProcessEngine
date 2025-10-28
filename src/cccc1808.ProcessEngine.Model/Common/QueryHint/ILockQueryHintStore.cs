using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Common.QueryHint
{
    public interface ILockQueryHintStore
    {
        bool TryGetCurrent(out TableHintScopeContainer container);

        TableHintScopeContainer StartScope(LockHintEnum hint);

        #region Types

        public class TableHintScopeContainer
            : IDisposable
        {
            private readonly Action _onScopeDisposed;
            public LockHintEnum Value { get; }
            public int? Take { get; }


            public TableHintScopeContainer(
                LockHintEnum value,
                int? take,
                Action onScopeDisposed
                )
            {
                _onScopeDisposed = onScopeDisposed;
                Value = value;
                Take = take;
            }

            public void Dispose()
            {
                _onScopeDisposed();
            }
        }

        #endregion
    }
}
