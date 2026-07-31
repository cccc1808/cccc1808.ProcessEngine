using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule
{
    /// <summary>
    /// Содержит буфер данных о зарезервированных всеми нодами процессах.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IProcessReservationState<TId>
    {
        ISet<TId> GetAll();

        void Reserve(TId processId, DateTimeOffset timeout);

        void Unreserve(TId procesId);

        void ClearTimeout(DateTimeOffset date);
    }
}
