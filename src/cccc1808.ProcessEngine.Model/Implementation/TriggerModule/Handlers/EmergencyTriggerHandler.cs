using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Setters;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers
{
    /// <summary>
    /// Универсальный страхующий триггер.
    /// Проверяет все не корневые range триггеры, которые давно не выполнялись.
    /// </summary>
    public class EmergencyTriggerHandler<TId>
        : ITriggerSingleHandler<TId>
    {
        public static string Name
            => nameof(EmergencyTriggerHandler<TId>);

        private readonly IServiceProvider _serviceProvider;
        private readonly IDateTimeProvider _dateTimeProvider;

        private readonly OptionsDto _options;

        public EmergencyTriggerHandler(
            IServiceProvider serviceProvider,
            IDateTimeProvider dateTimeProvider, 

            OptionsDto options)
        {
            _serviceProvider = serviceProvider;
            _dateTimeProvider = dateTimeProvider;

            _options = options;
        }

        public async ValueTask<ITriggerHandler.ResultDto> HandleAsync(
            ITriggerComponent<TId> trigger, 
            CancellationToken cancellationToken)
        {
            var softTimeout = _dateTimeProvider.UtcNow + _options.SoftTimeout;
            var timeout = _dateTimeProvider.UtcNow - _options.LostTriggerTimeout;

            while (true)
            {
                await using (var scope = _serviceProvider.CreateAsyncScope())
                {
                    var result = await ExecuteAsync(
                        scope.ServiceProvider, 
                        timeout, 
                        _options.BatchSize, 
                        cancellationToken);

                    if (!result)
                    {
                        break;
                    }
                }

                if (_dateTimeProvider.UtcNow >= softTimeout)
                {
                    return ITriggerHandler.ResultDto.ActivateResult();
                }
            }

            return ITriggerHandler.ResultDto.ActivateResult(
                _dateTimeProvider.UtcNow + _options.LostTriggerTimeout);
        }

        private static async Task<bool> ExecuteAsync(
            IServiceProvider serviceProvider,
            DateTimeOffset timeout,
            int batchSize,
            CancellationToken cancellationToken)
        {
            var options = serviceProvider.GetRequiredService<OptionsDto>();
            var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
            var query = serviceProvider.GetRequiredService<IQueries>();
            var triggerHandlerFactory = serviceProvider.GetRequiredService<ITriggerHandlerFactory<TId>>();
            var triggerRepository = serviceProvider.GetRequiredService<ITriggerRepository<TId>>();
            var setter = serviceProvider.GetRequiredService<ITriggerSetter<TId>>();

            await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
            {
                var triggers = await query.LoadAsync(
                    options.IgnoreHandlers,
                    timeout,
                    batchSize,
                    cancellationToken);
                
                if (!triggers.Any())
                {
                    return false;
                }

                foreach (var elem in triggers.GroupBy(e => e.HandlerKey))
                {
                    var handler = (ITriggerRangeHandler<TId>)triggerHandlerFactory.GetHandler(serviceProvider, elem.Key);
                    var result = await handler.CheckAsync(
                        elem, 
                        isEmergencyTrigger: true,
                        cancellationToken);

                    var forExecute = new List<ITriggerComponent<TId>>(result.Count);
                    foreach (var elem2 in elem)
                    {
                        var elem2Result = result[elem2.Key];
                        setter.StandartSetter.SetTriggerResult(
                            elem2,
                            elem2Result.Result);

                        if (elem2Result.NeedExecute)
                        {
                            forExecute.Add(elem2);
                        }
                    }
                    await handler.ExecuteAsync(forExecute, cancellationToken);
                }

                await triggerRepository.SaveAsync(triggers, cancellationToken);
                await transaction.CommitAsync(cancellationToken);                
            }

            return true;
        }

        public interface IQueries
        {
            Task<ICollection<ITriggerComponent<TId>>> LoadAsync(
                ISet<string> ignoreHandlers,
                DateTimeOffset timeout,
                int batchSize,
                CancellationToken cancellationToken);
        }

        public class OptionsDto 
        {
            public TimeSpan SoftTimeout { get; set; }
                = TimeSpan.FromMinutes(1);

            public TimeSpan LostTriggerTimeout { get; set; }
                = TimeSpan.FromMinutes(10);

            public int BatchSize { get; set; }
                = 100;

            /// <summary>
            /// Для триггер обрабатывается собсветнным страхующим триггером или ему не нужен страхующий триггер.
            /// </summary>
            public HashSet<string> IgnoreHandlers { get; set; }
                = new HashSet<string>();
        }
    }
}
