using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordAuditPlugin;

public class DiscordAuditModule : AssettoServerModule<DiscordConfiguration>
{
    public override void ConfigureDependencies(IServiceCollection services)
    {
        services.AddAutoActivatedSingleton<Discord>();
    }
}
