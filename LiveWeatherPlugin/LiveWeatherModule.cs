using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace LiveWeatherPlugin;

public class LiveWeatherModule : AssettoServerModule<LiveWeatherConfiguration>
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingletonHostedService<LiveWeatherProvider>();
    }
}
