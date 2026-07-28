using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto
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

            /// <summary>
            /// Это stream триггер.
            /// (Его нужно оповестить о том, что процесс перешел в состояние ожидания).
            /// </summary>
            public bool IsStreamTrigger { get; init; }

            /// <summary>
            /// Удалить триггер при завершении любого из указанных действий.
            /// </summary>
            public bool RemoveIfActionComplete { get; init; }

            public string[] RemoveActionIds { get; init; }

            /// <summary>
            /// Удалить триггер, если происходит переход на другой токен.
            /// </summary>
            public bool RemoveIfTokenMove { get; init; }

            public string? RemoveTokenId { get; init; }            

            /// <summary>
            /// Удалить триггер, если процесс завершен.
            /// </summary>
            public bool RemoveIfProcessComplete { get; init; }            

            public TriggerInfo(
                string key,
                bool isStreamTrigger,

                bool removeIfTokenMove,
                string? removeTokenId,

                bool removeIfActionComplete,
                string[] removeActionIds,
                
                bool removeIfProcessComplete)
            {
                Key = key;
                IsStreamTrigger = isStreamTrigger;

                RemoveIfTokenMove = removeIfTokenMove;
                RemoveTokenId = removeTokenId;

                if (removeIfActionComplete)
                {
                    if (!removeActionIds.Any())
                    {
                        throw new ArgumentException(nameof(removeActionIds));
                    }
                }

                RemoveIfActionComplete = removeIfActionComplete;
                RemoveActionIds = removeActionIds;

                RemoveIfProcessComplete = removeIfProcessComplete;                
            }
        }
    }
}
