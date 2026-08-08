using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule.Dto
{
    public readonly record struct ReservationMessageDto<TId>(
        TId ProcessId,
        DateTimeOffset? Timeout,
        bool IsReserveOrUnreserve);
}
