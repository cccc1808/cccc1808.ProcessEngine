using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.StaticInstance.Abstract.Dtos;
using cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Entities;
using cccc1808.ProcessEngine.Model.StaticInstance.Implementation.Services;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.StaticInstance.EF.Implementation.Storage.Queries
{
    public class EFStaticInstanceDeployServiceQueries<TId> 
        : StaticInstanceDeployService<TId>.IQueries
    {
        private readonly IIdGenerator<TId> _idGenerator;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IEFDbContext _dbContext;

        public EFStaticInstanceDeployServiceQueries(
            IIdGenerator<TId> idGenerator, 
            ILockQueryHintStore lockQueryHintStore, 
            IEFDbContext dbContext)
        {
            _idGenerator = idGenerator;
            _lockQueryHintStore = lockQueryHintStore;
            _dbContext = dbContext;
        }

        #region IQueries

        public StaticInstanceDeployService<TId>.IQueries.IContext PrepareContext(short deployVersion)
        {
            return new Context() 
            {
                DeployVersion = deployVersion,
            };
        }

        public async Task CreateOrTryGetDeployWithLockAsync(
            StaticInstanceDeployService<TId>.IQueries.IContext context,
            CancellationToken cancellationToken)
        {
            var typedContext = TypedContextRequired(context);

            var insertResult = await _dbContext
                .Set<StaticInstanceDeployDbEntity>()
                .Upsert(
                    new StaticInstanceDeployDbEntity(
                        0, 
                        typedContext.DeployVersion
                        )
                    )
                .On(e => e.Id)
                .NoUpdate()
                .RunAndReturnAsync(cancellationToken);

            if (insertResult.Any())
            {
                typedContext.DeployDbEntity = insertResult.Single();
                typedContext.DeployCreated = true;
                typedContext.DbDeploy = new StaticInstanceDeployService<TId>.IQueries.DeployDto(
                    typedContext.DeployDbEntity.Version);
                return;
            }

            using (_ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                var lockedExistsDeploy = await _dbContext
                    .Set<StaticInstanceDeployDbEntity>()
                    .FirstOrDefaultAsync(cancellationToken);

                if (lockedExistsDeploy is not null)
                {
                    typedContext.DeployDbEntity = lockedExistsDeploy;
                    typedContext.DbDeploy = new StaticInstanceDeployService<TId>.IQueries.DeployDto(
                        lockedExistsDeploy.Version);
                }

                typedContext.DeployCreated = false;
            }
        }

        public async Task<IReadOnlySet<StaticInstanceDeployService<TId>.IQueries.RegistrationInfo>> LoadRegistrationsAsync(
            StaticInstanceDeployService<TId>.IQueries.IContext context,
            CancellationToken cancellationToken)
        {
            var typedContext = TypedContextRequired(context);

            if (typedContext.DbDeploy is null)
            {
                throw new InvalidOperationException();
            }

            if (typedContext.DbRegistrationsEntities is not null)
            {
                throw new InvalidOperationException();
            }

            var exsistRegistrations = await _dbContext
                .Set<StaticInstanceRegistrationDbEntity<TId>>()
                .ToArrayAsync(cancellationToken);

            var dbRegistrationsEntities = new Dictionary<StaticInstanceProcessRegistrationDto, StaticInstanceRegistrationDbEntity<TId>>(exsistRegistrations.Length);
            var dbRegisrations = new HashSet<StaticInstanceDeployService<TId>.IQueries.RegistrationInfo>(exsistRegistrations.Length);

            foreach (var elem in exsistRegistrations)
            {
                var key = new StaticInstanceProcessRegistrationDto(
                    elem.ProcessType, 
                    elem.InstanceKey);

                dbRegistrationsEntities.Add(key, elem);
                dbRegisrations.Add(
                    new StaticInstanceDeployService<TId>.IQueries.RegistrationInfo(
                        key, 
                        elem.ProcessId
                        )
                    );
            }

            typedContext.DbRegistrationsEntities = dbRegistrationsEntities;
            return dbRegisrations;
        }

        public async Task<ICollection<TId>> TryLockProcessesAsync(
            StaticInstanceDeployService<TId>.IQueries.IContext context,
            ICollection<TId> processIds, 
            CancellationToken cancellationToken)
        {
            var typedContext = TypedContextRequired(context);

            using (_ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
            {
                var ids = await _dbContext.Set<ProcessDbEntity<TId>>()
                    .Where(e => processIds.Contains(e.Id))
                    .Select(e => e.Id)
                    .ToArrayAsync(cancellationToken);

                return ids;
            }
        }

        public Task RemoveRegistrationsAsync(
            StaticInstanceDeployService<TId>.IQueries.IContext context,
            ICollection<StaticInstanceProcessRegistrationDto> keys,
            CancellationToken cancellationToken)
        {
            var typedContext = TypedContextRequired(context);

            if (typedContext.DbDeploy is null)
            {
                throw new InvalidOperationException();
            }

            if (typedContext.DbRegistrationsEntities is null)
            {
                throw new InvalidOperationException();
            }
            
            _dbContext
                .Set<StaticInstanceRegistrationDbEntity<TId>>()
                .RemoveRange(
                    keys.Select(e => typedContext.DbRegistrationsEntities[e])
                    );

            return Task.CompletedTask;
        }        

        public async Task CreateRegistrationsAsync(
            StaticInstanceDeployService<TId>.IQueries.IContext context,
            ICollection<StaticInstanceDeployService<TId>.IQueries.RegistrationInfo> keys,
            CancellationToken cancellationToken)
        {
            var typedContext = TypedContextRequired(context);

            var ids = await _idGenerator.NextRangeAsync(keys.Count, cancellationToken);

            _dbContext
                .Set<StaticInstanceRegistrationDbEntity<TId>>()
                .AddRange(
                    keys.Select(
                        e => new StaticInstanceRegistrationDbEntity<TId>(
                            ids.Dequeue(),
                            e.StaticInstanceRegistration.ProcessType,
                            e.StaticInstanceRegistration.Key,
                            e.ProcessId)
                        )
                );
        }

        public async Task UpdateDeployVersionAsync(
            StaticInstanceDeployService<TId>.IQueries.IContext context,
            CancellationToken cancellationToken)
        {
            var typedContext = TypedContextRequired(context);

            if (typedContext.DeployDbEntity is null)
            {
                throw new InvalidOperationException();
            }

            typedContext.DeployDbEntity.Version = typedContext.DeployVersion;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        #endregion

        private static Context TypedContextRequired(
            StaticInstanceDeployService<TId>.IQueries.IContext context)
        {
            if (context is not Context typedContext)
            {
                throw new ArgumentException(nameof(context));
            }

            return typedContext;
        }

        #region types

        private class Context
            : StaticInstanceDeployService<TId>.IQueries.IContext
        {
            public required short DeployVersion { get; init; }
            
            public StaticInstanceDeployDbEntity? DeployDbEntity { get; set; }
            public StaticInstanceDeployService<TId>.IQueries.DeployDto? DbDeploy { get; set; }
            public bool DeployCreated { get; set; }

            public Dictionary<StaticInstanceProcessRegistrationDto, StaticInstanceRegistrationDbEntity<TId>>? DbRegistrationsEntities { get; set; }      
        }

        #endregion
    }
}
