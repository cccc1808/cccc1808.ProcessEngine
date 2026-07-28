using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.CommonModule.Storage
{
    public class RedisConnectionFactory
        : IRedisConnectionFactory
    {
        private readonly ConcurrentDictionary<string, LockContainer<RedisConnection>> _buffer;

        private readonly OptionsDto _options;

        public RedisConnectionFactory(OptionsDto options)
        {
            _buffer = new ConcurrentDictionary<string, LockContainer<RedisConnection>>();

            _options = options;
        }

        public async ValueTask<IRedisConnection> GetAsync(
            string name, 
            CancellationToken cancellationToken)
        {
            var container = _buffer.GetOrAdd(name, (_) => new LockContainer<RedisConnection>());

            var connection = await container.DoubleCheckPatternAsync(
                (name, _options),
                static (p, e) => e is not null,
                static async (p, t) => 
                {
                    var connectionOptions = p._options.ConnectionConfigrations[p.name];

                    return new RedisConnection(
                        await ConnectionMultiplexer.ConnectAsync(connectionOptions.ConnectionString),
                        connectionOptions.PiplineTimeout
                        );
                },
                cancellationToken);

            return connection;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var elem in _buffer.Values)
            {
                await elem.Write(
                    1,
                    static async (p, e, t) => 
                    {
                        await e.DisposeAsync();
                        return null;
                    },
                    CancellationToken.None
                    );

                elem.Dispose();
            }
        }

        public class OptionsDto
        {
            public required Dictionary<string, (string ConnectionString, TimeSpan PiplineTimeout)> ConnectionConfigrations { get; set; }
        }
    }
}
