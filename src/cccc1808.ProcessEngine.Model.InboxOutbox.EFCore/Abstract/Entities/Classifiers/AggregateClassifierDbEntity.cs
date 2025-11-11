using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.Entities.Classifiers
{
    /// <summary>
    /// Справочник агрегатов.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class AggregateClassifierDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }

        public string AggregateType { get; set; }
        public string AggregateId { get; set; }
    }
}
