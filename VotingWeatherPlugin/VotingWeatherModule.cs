using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace VotingWeatherPlugin;

public class VotingWeatherModule : AssettoServerModule<VotingWeatherConfiguration>
{
    public override void ConfigureDependencies(IServiceCollection services)
    {
        services.AddSingletonHostedService<VotingWeather>();
    }
}
