using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.Dto.Components
{
    public class CurrentSessionComponent
        : ICurrentSessionComponent
    {
        public Guid SessionId { get; set; }
        public bool IsSessionFirstStep { get; set; }
        public bool HaveError { get; set; }
        public DateTimeOffset? CreateRetryTimer { get; set; }
        public bool RetryTimerCreated { get; set; }

        public short ReTryLimit { get; set; }
    }
}
