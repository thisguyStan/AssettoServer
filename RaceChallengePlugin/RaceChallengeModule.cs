using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace RaceChallengePlugin;

public class RaceChallengeModule : AssettoServerModule
{
    public override void ConfigureDependencies(IServiceCollection services)
    {
        services.AddSingletonHostedService<RaceChallengePlugin>();
        services.AddTransient<EntryCarRace>();
        services.AddTransient<Race>();
    }
}
