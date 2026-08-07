using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.T2;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Test2.Infrastructure
{
    internal static class NameFactory
    {
        public static string ProcessQueue { get; }
            = "process_queue";

        public static string ProcessReserve { get; }
            = "process_reserve";

        public static string ProcessQueueChannel { get; }
            = "process_queue_channel";

        public static string TriggerQueue { get; }
            = "trigger_queue";

        public static string TriggerReserve { get; }
            = "trigger_reserve";

        public static string TriggerQueueChannel { get; }
            = "trigger_queue_channel";

        public static string IdToString(Guid id) => id.ToString();

        public static Guid StringToId(string name) => new Guid(name);

        public static string ProcessToKey(
            ProcessRegistryDto processRegistry, 
            string prefix)
        {
            return $"{prefix}{NameConst.NamePartsSplitChar}{processRegistry.Unique.ProcessType.ProcessType}{NameConst.NamePartsSplitChar}{processRegistry.Unique.ProcessType.ProcessVersion}{NameConst.NamePartsSplitChar}{processRegistry.Unique.Priority}";
        }

        public static ProcessRegistryDto KeyToProcessType(
            IServiceProvider serviceProvider,
            string key)
        {
            var processRegistry = serviceProvider.GetRequiredService<IProcessRegistry>();
            var parts = key.Split(NameConst.NamePartsSplitChar);

            return processRegistry.Get(
                new ProcessTypeUniqueDto(
                    new ProcessTypeDto(
                        long.Parse(parts[1]), 
                        int.Parse(parts[2])
                        ),
                    short.Parse(parts[3])
                    )
                );
        }

        public static string TriggerTypeToKey(IRedisNotifyTriggerQueueState.KeyDto key, string prefix)
        {
            return $"{prefix}{NameConst.NamePartsSplitChar}{key.HandlerName}{NameConst.NamePartsSplitChar}{key.Priority}";
        }

        public static IRedisNotifyTriggerQueueState.KeyDto KeyToTriggerType(string key)
        {
            var parts = key.Split(NameConst.NamePartsSplitChar);
            return new IRedisNotifyTriggerQueueState.KeyDto(
                parts[1], 
                short.Parse(parts[2])
                );
        }
    }
}
