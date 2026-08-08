using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.StaticInstance.Abstract.Dtos;
using cccc1808.ProcessEngine.Model.StaticInstance.Abstract.Handlers;

namespace cccc1808.ProcessEngine.Test2.TestGroup6.Infrastructure
{
    internal class StaticInstanceTestHandler
        : IStaticInstanceHandler<Guid>
    {
        private readonly IEFDbContext _dbContext;

        public StaticInstanceTestHandler(IEFDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public bool CanProcess(StaticInstanceProcessRegistrationDto staticInstanceRegistration)
        {
            if (staticInstanceRegistration.ProcessType == 1 && staticInstanceRegistration.Key == string.Empty)
            {
                return true;
            }

            return false;
        }

        public Task<IDictionary<StaticInstanceProcessRegistrationDto, Guid>> CreateProcessRangeAsync(
            ICollection<StaticInstanceProcessRegistrationDto> keys,
            CancellationToken cancellationToken)
        {
            var data = keys
                .ToDictionary(
                    e => e,
                    e =>
                    {
                        if (e.ProcessType == 1 && e.Key == string.Empty)
                        {
                            return new ProcessDbEntity<Guid>(
                                Guid.NewGuid(),
                                e.ProcessType,
                                1,
                                1,
                                stoppedByError: false,
                                status: ProcessStatusEnum.AsyncExecute,
                                retryCount: 0);
                        }

                        throw new NotImplementedException(e.ToString());
                    });

            _dbContext
                .Set<ProcessDbEntity<Guid>>()
                .AddRange(data.Values);

            return Task.FromResult(
                (IDictionary<StaticInstanceProcessRegistrationDto, Guid>)data.ToDictionary(
                    e => e.Key,
                    e => e.Value.Id)
                );
        }

        public Task RemoveProcessRangeAsync(
            ICollection<KeyValuePair<StaticInstanceProcessRegistrationDto, Guid>> keys, 
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
