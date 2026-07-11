using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Dtos;
using cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Handlers;
using cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Services;
using cccc1808.ProcessEngine.Model.StaticInstance.EF.Implementation.Services;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.StaticInstance.EF
{
    public static class StaticInstanceServiceCollectionExtension
    {
        public static IServiceCollection RegistryEFStaticInstance<TId, THandler>(
            this IServiceCollection services,
            EFStaticInstanceRunner.OptionsDto runnerOptions,
            StaticInstanceDeployRegistrationDto deployRegistration,
            params StaticInstanceProcessRegistrationDto[] processRegistrations
            )
            where THandler : class, IStaticInstanceHandler<TId>
        {
            services
                .AddScoped<IStaticInstanceDeployService, EFStaticInstanceDeployService<TId>>()
                .AddSingleton<IStaticInstanceRegistry, StaticInstanceRegistry>()
                .AddTransient<EFStaticInstanceRunner>()
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
