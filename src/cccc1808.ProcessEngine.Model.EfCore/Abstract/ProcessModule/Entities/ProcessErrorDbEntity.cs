using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities
{
    /// <summary>
    /// Хранение подробной информации о текущей ошибке процесса.
    /// Вынес в отдельную таблицу т.к. не требуется для обработки и чтобы не раздувать таблицу текстом ошибки.
    /// Можно отключить, и использовать только логи.
    /// </summary>
    public class ProcessErrorDbEntity<TId>
        : IId<TId>,
        IProcessLinked<TId>
    {
        public TId Id { get; set; } = default!;

        public TId ProcessId { get; set; } = default!;
        public ProcessDbEntity<TId> Process { get; set; } = default!;

        public JsonElement? Error { get; set; }

        public DateTimeOffset? ErrorDate { get; set; }

        public Guid? ErrorSessionId { get; set; }

        public ProcessErrorDbEntity() 
        { 
        }

        public ProcessErrorDbEntity(
            TId id, 
            TId processId,
            JsonElement? error = null,
            DateTimeOffset? errorDate = null,
            Guid? errorSessionId = null)
        {
            Id = id;
            ProcessId = processId;
            Error = error;
            ErrorDate = errorDate;
            ErrorSessionId = errorSessionId;
        }
    }
}
