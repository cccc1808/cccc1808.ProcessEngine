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
        private readonly ProcessRegistryDto[] _registrations;
        private readonly IDictionary<ProcessTypeDto, bool> _useSignalCode;

        public ProcessRegistry(IEnumerable<ProcessRegistryDto> registrations)
        {
            var registrationsCheck = new HashSet<(long, int, short)>();
            var processTypes = new Dictionary<ProcessTypeDto, bool>();

            foreach (var elem in registrations)
            {
                var key = (elem.ProcessType.ProcessType, elem.ProcessType.ProcessVersion, elem.Priority);

                if (registrationsCheck.Contains(key))
                {
                    throw new ArgumentException();
                }

                registrationsCheck.Add(key);

                if (!processTypes.TryGetValue(elem.ProcessType, out var exsist))
                {
                    processTypes.Add(elem.ProcessType, elem.UseSignal);
                }
                else 
                {
                    if (exsist != elem.UseSignal)
                    {
                        throw new ArgumentException();
                    }
                }
            }

            _registrations = registrations.ToArray();
            _useSignalCode = processTypes.ToFrozenDictionary();
        }

        public ICollection<ProcessRegistryDto> All()
        {
            return _registrations;
        }

        public bool UseSignalCode(ProcessTypeDto registryKey)
        {
            return _useSignalCode[registryKey];
        }
    }
}
