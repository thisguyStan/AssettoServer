using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace TagModePlugin;

public class TagModeModule : AssettoServerModule<TagModeConfiguration>
{
    public override void ConfigureDependencies(IServiceCollection services)
    {
        services.AddSingletonHostedService<TagModePlugin>();
        services.AddTransient<EntryCarTagMode>();
        services.AddTransient<TagSession>();
    }
}
