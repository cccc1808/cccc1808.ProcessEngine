using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites
{
    /// <summary>
    /// Хранение подробной информации о текущей ошибке процесса.
    /// Можно отключить, и использовать только логи.
    /// </summary>
    public class ProcessErrorDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }

        public JsonElement? Error { get; set; }
    }
}
