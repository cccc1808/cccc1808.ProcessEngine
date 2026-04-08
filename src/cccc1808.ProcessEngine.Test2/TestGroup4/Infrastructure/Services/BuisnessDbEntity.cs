using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;

namespace cccc1808.ProcessEngine.Test2.TestGroup4.Infrastructure.Services
{
    internal class BuisnessDbEntity
        : IId<Guid>
    {
        public Guid Id { get; set; }

        public int Counter { get; set; }
    }
}
