using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Entities
{
    /// <summary>
    /// Справочник агрегатов.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class AggregateClassifierDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; } = default!;

        public string AggregateType { get; set; } = default!;

        public string AggregateId { get; set; } = default!;
    }
}
