using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Test2.TestGroup4.Infrastructure.Services
{
    public class MessagesStateComponent<TState>
    {
        public Dictionary<string, TState> State { get; set; }
            = new Dictionary<string, TState>();
    }
}
