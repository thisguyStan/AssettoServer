using AssettoServer.Server;
using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace TagModePlugin;

public class TagModeModule : AssettoServerModule<TagModeConfiguration>
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingletonHostedService<TagModePlugin>();
        services.AddTransientFactory<Func<EntryCar, EntryCarTagMode>>();
        services.AddTransientFactory<TagSession.Factory>();
    }
}
