using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordAuditPlugin;

public class DiscordAuditModule : AssettoServerModule<DiscordConfiguration>
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAutoActivatedSingleton<Discord>();
    }
}
