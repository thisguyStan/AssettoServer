using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace ReplayPlugin;

public class ReplayModule : AssettoServerModule<ReplayConfiguration>
{
    public override void ConfigureDependencies(IServiceCollection services)
    {
        services.AddSingletonHostedService<ReplayPlugin>();
        services.AddSingletonHostedService<ReplayService>();
        services.AddSingleton<ReplayWriter>();
        services.AddSingleton<ReplaySegmentManager>();
        services.AddSingleton<EntryCarExtraDataManager>();
        services.AddSingleton<ReplayMetadataProvider>();
    }
}
