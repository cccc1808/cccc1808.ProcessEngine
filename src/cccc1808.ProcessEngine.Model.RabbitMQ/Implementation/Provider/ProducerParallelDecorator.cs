using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.RabbitMQ.Abstract.Provider;

namespace cccc1808.ProcessEngine.Model.RabbitMQ.Implementation.Provider
{
    public class ProducerParallelDecorator 
        : IRabbitMqProducer
    {
        private readonly IRabbitMqProducer _source;

        private readonly SemaphoreSlim _paralellLimit 
            = new SemaphoreSlim(1, 1);

        public ProducerParallelDecorator(IRabbitMqProducer source)
        {
            _source = source;
        }
        
        public async Task ProduceBatchAsync(
            ICollection<MessageDto> messages,
            CancellationToken cancellationToken)
        {
            await _paralellLimit.WaitAsync(cancellationToken);
            try 
            {
                await _source.ProduceBatchAsync(messages, cancellationToken);
            }
            finally 
            {
                _paralellLimit.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _source.DisposeAsync();
        }
    }
}
