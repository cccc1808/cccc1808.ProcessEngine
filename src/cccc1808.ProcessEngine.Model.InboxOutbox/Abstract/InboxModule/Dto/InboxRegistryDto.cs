using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Dto
{
    /// <summary>
    /// TODO: версии.
    /// </summary>
    /// <param name="Registry"></param>
    public record InboxRegistryDto(
        ProcessTypeUniqueDto Unique,
        string TriggerEventQueue)
    {
    }
}
