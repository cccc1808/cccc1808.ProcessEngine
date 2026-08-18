using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.Queue;

namespace cccc1808.ProcessEngine.Test2.Infrastructure
{
    internal static class NameFactory
    {
        public static string ProcessQueue { get; }
            = "process_queue";

        public static string ProcessQueueReserve { get; }
            = "process_reserve";

        public static string ProcessSelectReserve { get; }
            = "process_select_reseve";

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
            ProcessTypeUniqueDto processRegistry, 
            string prefix)
        {
            return $"{prefix}{NameConst.NamePartsSplitChar}{processRegistry.ProcessType.ProcessType}{NameConst.NamePartsSplitChar}{processRegistry.ProcessType.ProcessVersion}{NameConst.NamePartsSplitChar}{processRegistry.Priority}";
        }

        public static ProcessTypeUniqueDto KeyToProcessType(
            string key)
        {
            var parts = key.Split(NameConst.NamePartsSplitChar);

            return new ProcessTypeUniqueDto(
                new ProcessTypeDto(
                    long.Parse(parts[1]),
                    int.Parse(parts[2])
                    ),
                short.Parse(parts[3])
                );
        }

        public static string TriggerTypeToKey(IRedisTriggerQueueNotifyState.KeyDto key, string prefix)
        {
            return $"{prefix}{NameConst.NamePartsSplitChar}{key.HandlerName}{NameConst.NamePartsSplitChar}{key.Priority}";
        }

        public static IRedisTriggerQueueNotifyState.KeyDto KeyToTriggerType(string key)
        {
            var parts = key.Split(NameConst.NamePartsSplitChar);
            return new IRedisTriggerQueueNotifyState.KeyDto(
                parts[1], 
                short.Parse(parts[2])
                );
        }
    }
}
