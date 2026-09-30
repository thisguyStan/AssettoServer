using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace GeoIPPlugin;

public class GeoIPModule : AssettoServerModule<GeoIPConfiguration>
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAutoActivatedSingleton<GeoIP>();
    }
}
