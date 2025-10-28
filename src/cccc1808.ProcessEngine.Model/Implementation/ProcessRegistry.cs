using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto.Registry;
using cccc1808.ProcessEngine.Model.Abstract.Services;

namespace cccc1808.ProcessEngine.Model.Implementation
{
    public class ProcessRegistry
        : IProcessRegistry
    {
        private readonly ProcessRegistryDto[] _registrations;

        public ProcessRegistry(IEnumerable<ProcessRegistryDto> registrations)
        {
            var doubl = registrations
                .GroupBy(e => e)
                .FirstOrDefault(e => e.Count() > 1);
            if (doubl != null)
            {
                throw new ArgumentException();
            }

            _registrations = registrations.ToArray();
        }

        public ICollection<ProcessRegistryDto> All()
        {
            return _registrations;
        }
    }
}
