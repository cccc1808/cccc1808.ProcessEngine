using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities;

namespace cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Dto.Components
{
    internal class MessageStreamComponent<TId>
    {
        public TimerProcessDbEntity<TId> StreamProcessDbEntity { get; init; }
        // public StreamProcessDataDbEntity<TId> StreamDataDbEntity { get; init; }
    }
}
