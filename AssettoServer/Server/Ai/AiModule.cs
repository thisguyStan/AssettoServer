using System;
using AssettoServer.Server.Ai.Splines;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.OpenSlotFilters;
using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AssettoServer.Server.Ai;

public class AiModule : AssettoServerModule
{
    private readonly ACServerConfiguration _configuration;

    public AiModule(ACServerConfiguration configuration)
    {
        _configuration = configuration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransientFactory<Func<EntryCar, AiState>>();

        if (_configuration.Extra.EnableAi)
        {
            services.AddSingletonHostedService<AiBehavior>();
            services.AddAutoActivatedSingleton<AiUpdater>();
            services.AddTransient<IOpenSlotFilter, AiSlotFilter>();

            if (_configuration.Extra.AiParams.HourlyTrafficDensity != null)
            {
                services.AddSingleton<IHostedService, DynamicTrafficDensity>();
            }

            services.AddTransient<AiSplineWriter>();
            services.AddTransient<FastLaneParser>();
            services.AddTransient<AiSplineLocator>();
            services.AddSingleton(provider => provider.GetRequiredService<AiSplineLocator>().Locate());
        }
    }
}
