using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.QueryHint;

namespace cccc1808.ProcessEngine.Model.Implementation.Storage
{
    public class LockQueryHintStore
        : ILockQueryHintStore
    {
        private ILockQueryHintStore.TableHintScopeContainer? _scopeContainer { get; set; }


        public bool TryGetCurrent(out ILockQueryHintStore.TableHintScopeContainer container)
        {
            container = _scopeContainer!;
            return container is not null;
        }

        public ILockQueryHintStore.TableHintScopeContainer StartScope(LockHintEnum hint)
        {
            if (TryGetCurrent(out _))
            {
                throw new NotSupportedException("Нельзя создать вложенный TableHintScope. Проверьте вложенность.");
            }

            _scopeContainer = new ILockQueryHintStore.TableHintScopeContainer(
                hint,
                null,
                () => _scopeContainer = null
            );

            return _scopeContainer;
        }
    }
}
