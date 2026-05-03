using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components
{
    /// <summary>
    /// Процесс при обработке сообщений стрима фиксирует последнее обработанное сообщение.
    /// При засыпании процесса, данные о смещении будут опубликованы в событии засыпания.
    /// </summary>
    public interface IOffsetStreamTriggerComponent
    {
        string TriggerKey { get; }

        IDictionary<string, long> ProcessedChannels { get; }


        void UpdateMaxTimestamp(
            string channelName,
            long timestamp);
    }
}
