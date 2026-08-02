using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services
{
    /// <summary>
    /// Реестр зарегистрированных триггеров.
    /// TODO: приоритет.
    /// </summary>
    public interface ITriggerRegistry
    {
        IReadOnlyCollection<TriggerRegistryDto> GetAll();

        Type GetHandlerType(string handler);

        bool TryGetHandlerType(string handler, out Type handlerType);
    }
}
