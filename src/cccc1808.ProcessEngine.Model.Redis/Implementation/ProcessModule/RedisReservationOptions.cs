using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.ProcessModule
{
    /// <summary>
    /// Опции резервирования процессов.
    /// </summary>
    public class RedisReservationOptions
    {
        public required string ConnectionName { get; set; }

        public required string ChannelName { get; set; }
    }
}
