using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider
{
    /// <summary>
    /// Резервирования слота на выборку в очередь.
    /// </summary>
    public interface ITriggerSelectorReserveProvider
    {
        Task<IReserveScope> TryReserveAsync(
            TriggerTypeUniqueDto triggerTypeUnique,
            DateTimeOffset date,
            CancellationToken cancellationToken);

        public interface IReserveScope 
            : IAsyncDisposable
        {
            bool IsSuccess { get; }

            DateTimeOffset Timeout { get; }

            /// <summary>
            /// Продлить резервирование на указанное значение.
            /// </summary>
            Task UpdateAsync(
                DateTimeOffset date,
                CancellationToken cancellationToken);

            /// <summary>
            /// Не снимать резервирование при завершении scope.
            /// </summary>
            void NoUnreserve();
        }
    }
}
