using AssettoServer.Server;
using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace AutoModerationPlugin;

public class AutoModerationModule : AssettoServerModule<AutoModerationConfiguration>
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingletonHostedService<AutoModerationPlugin>();
        services.AddTransientFactory<Func<EntryCar, EntryCarAutoModeration>>();
    }
}
