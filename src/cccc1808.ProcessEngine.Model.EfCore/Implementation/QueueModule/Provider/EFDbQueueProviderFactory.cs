using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.QueueModule.Storage;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.QueueModule.Provider
{
    public class EFDbQueueProviderFactory<TId> : IQueueProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public EFDbQueueProviderFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ValueTask<IQueueConsumer> GetConsumerAsync(string name, CancellationToken cancellationToken)
        {
            var options = _serviceProvider.GetRequiredService<EFDbQueueConsumer<TId>.OptionsDto>();
            options = new EFDbQueueConsumer<TId>.OptionsDto(name, options.EmptyTimeout, options.PackLimit);

            var consumer = new EFDbQueueConsumer<TId>(
                _serviceProvider.GetRequiredService<IEFDbContext>(), 
                _serviceProvider.GetRequiredService<ILockQueryHintStore>(),
                options
                );

            return ValueTask.FromResult<IQueueConsumer>(consumer);
        }

        public ValueTask<bool> DisconnectConsumerAsync(string name, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(true);
        }

        public ValueTask<IQueueProducer> GetProducerAsync(string name, CancellationToken cancellationToken)
        {
            var producer = new EFDbQueueProducer<TId>(
                _serviceProvider.GetRequiredService<IIdGenerator<TId>>(),
                _serviceProvider.GetRequiredService<IDateTimeProvider>(),
                _serviceProvider.GetRequiredService<IEFDbContext>(),
                _serviceProvider.GetRequiredService<EfDbQueueClassifier<TId>>()
                );

            return ValueTask.FromResult<IQueueProducer>(producer);
        }
    }
}
