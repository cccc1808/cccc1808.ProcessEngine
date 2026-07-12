using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.StaticInstance.Abstract.Dtos;
using cccc1808.ProcessEngine.Model.StaticInstance.Abstract.Handlers;
using cccc1808.ProcessEngine.Model.StaticInstance.Abstract.Services;
using cccc1808.ProcessEngine.Model.StaticInstance.EF.Implementation.Storage.Queries;
using cccc1808.ProcessEngine.Model.StaticInstance.Implementation.Services;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.StaticInstance.EF
{
    public static class StaticInstanceServiceCollectionExtension
    {
        public static IServiceCollection RegistryEFStaticInstance<TId, THandler>(
            this IServiceCollection services,
            StaticInstanceRunner.OptionsDto runnerOptions,
            StaticInstanceDeployRegistrationDto deployRegistration,
            params StaticInstanceProcessRegistrationDto[] processRegistrations
            )
            where THandler : class, IStaticInstanceHandler<TId>
        {
            services
                .AddScoped<IStaticInstanceDeployService, StaticInstanceDeployService<TId>>()
                .AddScoped<StaticInstanceDeployService<TId>.IQueries, EFStaticInstanceDeployServiceQueries<TId>>()
                .AddSingleton<IStaticInstanceRegistry, StaticInstanceRegistry>()
                .AddTransient<StaticInstanceRunner>()
                .AddSingleton(runnerOptions)
                .AddScoped<IStaticInstanceHandler<TId>, THandler>()
                ;

            services.AddSingleton(deployRegistration);
            foreach (var elem in processRegistrations)
            {
                services.AddSingleton(elem);
            }

            return services;
        }
    }
}
