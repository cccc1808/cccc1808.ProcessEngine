using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Services
{
    public class ProcessRegistry
        : IProcessRegistry
    {
        private readonly FrozenDictionary<ProcessTypeUniqueDto, ProcessRegistryDto> _dictionary;
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
            _dictionary = registrations.ToFrozenDictionary(e => e.Unique, e => e);
        }

        public ICollection<ProcessRegistryDto> All()
        {
            return _registrations;
        }

        public ProcessRegistryDto Get(ProcessTypeUniqueDto processTypeUnique)
        {
            return _dictionary[processTypeUnique];
        }
    }
}
