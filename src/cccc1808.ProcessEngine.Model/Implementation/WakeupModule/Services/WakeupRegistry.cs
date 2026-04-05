using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Implementation.WakeupModule.Services
{
    public class WakeupRegistry<TId> : IWakeupRegistry<TId>
    {
        private readonly IReadOnlyDictionary<ProcessTypeDto, Type> _checkHandlers;

        public WakeupRegistry(
            IServiceProvider serviceProvider,
            IEnumerable<WakeupRegistryDto> registries)
        {
            // Проверяем, что на каждый зарегистрированный тип процесса зарегистрирован хендлер.
            foreach (var elem in registries)
            {
                var typedHandler = (IWakeupCheckHandler<TId>)serviceProvider.GetRequiredService(elem.CheckWakeupHandlerType);
            }

            _checkHandlers = registries.ToDictionary(
                e => e.ProcessRegistry.ProcessType, 
                e => e.CheckWakeupHandlerType);
        }

        public IWakeupCheckHandler<TId> GetCheckHandler(
            IServiceProvider serviceProvider,
            ProcessTypeDto processType)
        {
            return (IWakeupCheckHandler<TId>)serviceProvider.GetRequiredService(_checkHandlers[processType]);
        }

        public bool IsWakeupProcess(ProcessTypeDto processType)
        {
            return _checkHandlers.ContainsKey(processType);
        }        
    }
}
