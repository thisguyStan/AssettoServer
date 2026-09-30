using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace CustomCommandPlugin;

public class CustomCommandModule : AssettoServerModule<CustomCommandConfiguration>
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingletonHostedService<CustomCommand>();
    }
}
