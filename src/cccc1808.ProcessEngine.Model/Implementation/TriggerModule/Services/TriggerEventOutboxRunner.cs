using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services.Events;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services
{
    public class TriggerEventOutboxRunner<TId> 
        : ITriggerEventOutboxRunner<TId>
    {
        private readonly IServiceProvider _serviceProvider;

        public TriggerEventOutboxRunner(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task RunAsync(
            bool oneCycle,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                await using (var scope = _serviceProvider.CreateAsyncScope())
                {
                    var options = scope.ServiceProvider.GetRequiredService<OptionsDto>();

                    try
                    {
                        var result = await CycleAsync(
                            scope.ServiceProvider,
                            cancellationToken);

                        if (!result)
                        {
                            await Task.Delay(options.EmptyTimeout, cancellationToken);
                        }
                        else 
                        {
                            if (oneCycle)
                            {
                                break;
                            }                            
                        }
                    }
                    catch (Exception ex)
                    {
                        if (OperationCancelHelper.IsCancelException(ex, cancellationToken))
                        {
                            throw;
                        }

                        // TODO: log

                        await Task.Delay(options.ExceptionTimeout, cancellationToken);
                    }
                }
            }
        }

        private async Task<bool> CycleAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            var options = serviceProvider.GetRequiredService<OptionsDto>();
            var query = serviceProvider.GetRequiredService<TriggerEventRaiserExceptionDbDecorator<TId>.IQuery>();
            var raiser = options.NoDecoratorEventRaiserFactory(serviceProvider);
            var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();

            await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
            {
                var events = await query.LoadForSendAsync(options.BatchSize, cancellationToken);
                if (!events.Any())
                {
                    return false;
                }

                await raiser.RaiseAsync(events, cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }

            return true;
        }

        public class OptionsDto
        {
            public required Func<IServiceProvider, ITriggerEventRaiser<TId>> NoDecoratorEventRaiserFactory { get; set; }

            public int BatchSize { get; set; }
                = 100;

            public TimeSpan EmptyTimeout { get; set; }
                = TimeSpan.FromSeconds(10);

            public TimeSpan ExceptionTimeout { get; set; }
                = TimeSpan.FromSeconds(30);
        }
    }
}
