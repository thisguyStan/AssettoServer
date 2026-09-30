using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace TimeDilationPlugin;

public class TimeDilationModule : AssettoServerModule<TimeDilationConfiguration>
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingletonHostedService<TimeDilationPlugin>();
    }
}
