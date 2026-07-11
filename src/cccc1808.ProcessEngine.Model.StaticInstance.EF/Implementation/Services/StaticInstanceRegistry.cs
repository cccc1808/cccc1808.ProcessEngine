using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Dtos;
using cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Services;

namespace cccc1808.ProcessEngine.Model.StaticInstance.EF.Implementation.Services
{
    internal class StaticInstanceRegistry : IStaticInstanceRegistry
    {
        private readonly StaticInstanceDeployRegistrationDto DeployRegistration;
        private readonly IReadOnlySet<StaticInstanceProcessRegistrationDto> ProcessRegistrations;

        public StaticInstanceRegistry(
            StaticInstanceDeployRegistrationDto deploytRegistration,
            IEnumerable<StaticInstanceProcessRegistrationDto> processRegistrations)
        {
            DeployRegistration = deploytRegistration;
            ProcessRegistrations = processRegistrations.ToHashSet();
        }

        public IReadOnlySet<StaticInstanceProcessRegistrationDto> All()
        {
            return ProcessRegistrations;
        }

        public short GetDeployVersion()
        {
            return DeployRegistration.Version;
        }
    }
}
