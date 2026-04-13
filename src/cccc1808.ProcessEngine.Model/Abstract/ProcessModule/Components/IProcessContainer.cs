using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components
{
    /// <summary>
    /// Контейнер содержиащие основные данные процесса,
    /// а также может содержать дополнительные данные конкретной реализации процесса.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IProcessContainer<TId>
    {
        /// <summary>
        /// Id процесса.
        /// </summary>
        TId Id { get; }

        /// <summary>
        /// Основной компонент <see cref="IProcessComponent<TId>"/>.
        /// </summary>
        IProcessComponent<TId> Process { get; }

        /// <summary>
        /// Компонент асинхронной обработки <see cref="IAsyncSessionComponent"/>.
        /// </summary>
        IAsyncSessionComponent CurrentSession { get; }

        /// <summary>
        /// Флаг говорит о том, загружено ли это состояние для выполнения асинхронной обработки или нет.
        /// </summary>
        bool InAsyncExecuting { get; }

        /// <summary>
        /// Процесс использует систему гарантированного пробуждения.
        /// </summary>
        bool UsingWakeup { get; }

        /// <summary>
        /// Добавить компонент.
        /// </summary>
        /// <param name="component"></param>
        void AddComponent<T>(T component);

        T GetComponent<T>();

        bool TryGetComponent<T>(out T result);

        /// <summary>
        /// Удалить компонент.
        /// </summary>
        void RemoveComponent<T>();
    }
}
