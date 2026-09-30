using AssettoServer.Server;
using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace RaceChallengePlugin;

public class RaceChallengeModule : AssettoServerModule
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingletonHostedService<RaceChallengePlugin>();
        services.AddTransientFactory<Func<EntryCar, EntryCarRace>>();
        services.AddTransientFactory<Race.Factory>();
    }
}
