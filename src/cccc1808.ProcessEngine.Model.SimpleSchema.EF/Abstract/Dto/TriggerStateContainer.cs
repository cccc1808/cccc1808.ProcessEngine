using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto
{
    /// <summary>
    /// Содержит данные о триггерах процесса.
    /// </summary>
    public class TriggerStateContainer
    {
        public Dictionary<string, TriggerInfo> Triggers { get; set; }
            = new Dictionary<string, TriggerInfo>();

        public readonly record struct TriggerInfo
        {
            public string Key { get; init; }

            public string RemoveTriggerQueueName { get; init; }

            public string? RemoveTokenId { get; init; }

            /// <summary>
            /// Удалить триггер, если происходит переход на другой токен.
            /// </summary>
            public bool RemoveIfTokenMove { get; init; }

            /// <summary>
            /// Удалить триггер, если процесс завершен.
            /// </summary>
            public bool RemoveIfProcessComplete { get; init; }

            public TriggerInfo(
                string key, 
                string removeTriggerQueueName,
                string? removeTokenId,
                bool removeIfTokenMove,
                bool removeIfProcessComplete)
            {
                Key = key;
                RemoveTriggerQueueName = removeTriggerQueueName;
                RemoveTokenId = removeTokenId;
                RemoveIfTokenMove = removeIfTokenMove;
                RemoveIfProcessComplete = removeIfProcessComplete;
            }
        }
    }

    public interface IProcessStateWithTriggers
    {
        TriggerStateContainer TriggerState { get; }
    }
}
