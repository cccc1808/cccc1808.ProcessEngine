using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.ExternalCounter;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule
{
    public class RedisExternalCounterProviderFactory 
        : IExternalCounterProviderFactory, 
        IAsyncDisposable
    {
        private readonly LockContainer<RedisExternalCounterProvider> _connectionContainer;

        private readonly OptionsDto _options;
        private readonly RedisExternalCounterProvider.OptionsDto _providerOptions;

        public RedisExternalCounterProviderFactory(
            OptionsDto options,
            RedisExternalCounterProvider.OptionsDto providerOptions)
        {
            _options = options;
            _connectionContainer = new LockContainer<RedisExternalCounterProvider>();
            _providerOptions = providerOptions;
        }

        public async ValueTask<IExternalCounterProvider> GetProviderAsync(
            CancellationToken cancellationToken)
        {
            var connection = await _connectionContainer.DoubleCheckPatternAsync(
                _options,
                (p, e) => e is not null,
                async (p, e) => new RedisExternalCounterProvider(
                    await ConnectionMultiplexer.ConnectAsync(p.ConnectinoString),
                    _providerOptions),
                cancellationToken);

            return connection;
        }

        public async ValueTask DisposeAsync()
        {
            await _connectionContainer.Write(
                1,
                async (p, e, t) => 
                {
                    if (e is not null)
                    {
                        await e.DisposeAsync();
                    }
                    return null!;
                },
                default);
            _connectionContainer.Dispose();
        }

        public class OptionsDto()
        {
            public required string ConnectinoString { get; set; }
        }
    }
}
