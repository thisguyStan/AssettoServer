using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace AutoModerationPlugin;

public class AutoModerationModule : AssettoServerModule<AutoModerationConfiguration>
{
    public override void ConfigureDependencies(IServiceCollection services)
    {
        services.AddSingletonHostedService<AutoModerationPlugin>();
        services.AddTransient<EntryCarAutoModeration>();
    }
}
