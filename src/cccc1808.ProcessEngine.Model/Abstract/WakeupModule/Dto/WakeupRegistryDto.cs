using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Handlers;

namespace cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto
{
    /// <summary>
    /// Регистрирует информацию о процессе, использующем wakeup.
    /// </summary>
    /// <param name="ProcessRegistry"></param>
    /// <param name="CheckWakeupHandlerType"><see cref="IWakeupCheckHandler{TId}"/>Тип хендлера.</param>
    public record WakeupRegistryDto(
        ProcessRegistryDto ProcessRegistry,
        WakeupStateEnum WakeupState,
        Type CheckWakeupHandlerType)
    {
    }
}
