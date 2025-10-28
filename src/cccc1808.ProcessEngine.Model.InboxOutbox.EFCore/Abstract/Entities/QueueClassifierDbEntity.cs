using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities
{
    /// <summary>
    /// Справочник очередей.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class QueueClassifierDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }

        public string Name { get; set; }
    }
}
