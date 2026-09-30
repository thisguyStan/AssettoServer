using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace VotingWeatherPlugin;

public class VotingWeatherModule : AssettoServerModule<VotingWeatherConfiguration>
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingletonHostedService<VotingWeather>();
    }
}
