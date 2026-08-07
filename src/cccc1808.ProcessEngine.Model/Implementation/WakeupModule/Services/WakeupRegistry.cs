using System;
using System.Collections.Frozen;
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
        private readonly IReadOnlyDictionary<ProcessTypeDto, (WakeupStateEnum State, Type CheckHandler)> _checkHandlers;

        public WakeupRegistry(
            IServiceProvider serviceProvider,
            IEnumerable<WakeupRegistryDto> registries)
        {
            // Проверяем, что на каждый зарегистрированный тип процесса зарегистрирован хендлер.
            using (var scope = serviceProvider.CreateScope())
            {
                foreach (var elem in registries)
                {
                    if (elem.WakeupState == WakeupStateEnum.NoWakeup)
                    {
                        throw new ArgumentException($"Регистрация {elem.Unique.ProcessType.ProcessType} не требуется.");
                    }

                    var typedHandler = (IWakeupCheckHandler<TId>)scope.ServiceProvider.GetRequiredService(elem.CheckWakeupHandlerType);
                }
            }
            
            _checkHandlers = registries.ToFrozenDictionary(
                e => e.Unique.ProcessType,
                e => (e.WakeupState, e.CheckWakeupHandlerType));
        }

        public IWakeupCheckHandler<TId> GetCheckHandler(
            IServiceProvider serviceProvider,
            ProcessTypeDto processType)
        {
            return (IWakeupCheckHandler<TId>)serviceProvider
                .GetRequiredService(_checkHandlers[processType].CheckHandler);
        }

        public WakeupStateEnum CheckWakeup(ProcessTypeDto processType)
        {
            if (_checkHandlers.TryGetValue(processType, out var registration))
            {
                return registration.State;
            }

            return WakeupStateEnum.NoWakeup;
        }        
    }
}
