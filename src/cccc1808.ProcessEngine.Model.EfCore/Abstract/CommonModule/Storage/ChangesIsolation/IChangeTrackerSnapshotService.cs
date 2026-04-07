using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage.ChangesIsolation
{
    /// <summary>
    ///  Используется для изоляции изменений <see cref="EntityFrameworkCore.Db"/>.
    ///  Небольшие снимки памяти производительнее чем savepoint.
    /// </summary>
    public interface IChangeTrackerSnapshotService
    {        
        ISubscribe CaptureState();

        #region Types

        public interface ISubscribe
            : IDisposable
        {
            /// <summary>
            /// Не выполнять сброс контекста
            /// </summary>
            void NoRestore();

            /// <summary>
            /// Выполнить сброс контекста (Вызывается при Dispose, если не было явного вызова NoRestore или Restore)
            /// </summary>
            void Restore();
        }

        #endregion
    }
}
