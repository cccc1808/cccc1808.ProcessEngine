using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components
{
    /// <summary>
    /// Позволяет делать снимок InMemory состояния, которое может меняться, но при этом не отслеживается EF.ChangeTracker для использования InMemory изоляции.
    /// Реализуется по необходимости компонентами, которые крепяться через <see cref="IProcessContainer.AddComponent{T}(T)"/>.
    /// Для <see cref="IIsolationService.IsolationMode.ChangeTrackerSnapshot"/>.
    /// </summary>
    public interface IInmemoryMutableState
    {
        /// <summary>
        /// Сформировать снимок InMemory состояния, которое может меняться.
        /// </summary>
        /// <returns></returns>
        ISnapshot Capture();

        /// <summary>
        /// Восстановить InMemory состояние из снимка.
        /// </summary>
        /// <param name="snapshot"></param>
        void Restore(ISnapshot snapshot);

        /// <summary>
        /// Котейнер снимок состояния.
        /// </summary>
        public interface ISnapshot { }
    }
}
