using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Storage
{
    /// <summary>
    ///  Используется для изоляции изменений <see cref="EntityFrameworkCore.Db"/>.
    ///  Небольшие снимки памяти производительнее чем savepoint.
    /// </summary>
    public interface IChangeTrackerSnapshotService
    {
        /// <summary>
        /// Создать снимок восстановления для текущего состояния
        /// </summary>
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
