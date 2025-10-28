using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites
{
    /// <summary>
    /// Хранение подробной информации о текущей ошибке процесса.
    /// Вынес в отдельную таблицу т.к. не требуется для обработки и чтобы не раздувать таблицу текстом ошибки.
    /// Можно отключить, и использовать только логи.
    /// </summary>
    public class ProcessErrorDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }

        public JsonElement? Error { get; set; }

        public DateTimeOffset? ErrorDate { get; set; }

        public Guid? ErrorSessionId { get; set; }

    }
}
