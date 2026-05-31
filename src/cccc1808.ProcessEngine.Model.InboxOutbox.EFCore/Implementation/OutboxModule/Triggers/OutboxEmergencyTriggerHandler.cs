using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Triggers
{
    /// <summary>
    /// Резервный хендлер, проталкивающий застрявшие процессы (по которым не сработал их персональный триггер).
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    internal class OutboxEmergencyTriggerHandler<TId>
        : ITriggerSingleHandler<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly EFTriggerHandlerFacade<TId> _handlerFacade;

        private readonly Options _options;

        public OutboxEmergencyTriggerHandler(
            IServiceProvider serviceProvider,
            IDateTimeProvider dateTimeProvider,
            EFTriggerHandlerFacade<TId> handlerFacade,

            Options options)
        {
            _serviceProvider = serviceProvider;
            _dateTimeProvider = dateTimeProvider;
            _handlerFacade = handlerFacade;

            _options = options;
        }

        public async ValueTask<ITriggerHandler.ResultDto> HandleAsync(
            ITriggerComponent<TId> trigger, 
            CancellationToken cancellationToken)
        {
            var result = await _handlerFacade.CustomEmergencyTriggerHandlerAsync(
                _serviceProvider,
                _dateTimeProvider.UtcNow.AddMinutes(1),
                _dateTimeProvider.UtcNow.AddMinutes(-30),
                batchSize: 50,
                (q, dbContext) => q.Where(e =>
                    dbContext.Set<OutboxMessageDbEntity<TId>>()
                        .Where(e2 =>
                            e2.ProcessId.Equals(e.Id)
                            && e2.IsActive)
                        .Any() // Есть необработанные сообщения.                               
                        ),
                cancellationToken
                );

            if (!result)
            {
                return ITriggerHandler.ResultDto.ActivateResult();
            }
            else 
            {
                return ITriggerHandler.ResultDto.ActivateResult(
                    _dateTimeProvider.UtcNow + TimeSpan.FromMinutes(10));
            }
        }

        public class Options
        {

        }
    }
}
