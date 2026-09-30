using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace TimeDilationPlugin;

public class TimeDilationModule : AssettoServerModule<TimeDilationConfiguration>
{
    public override void ConfigureDependencies(IServiceCollection services)
    {
        services.AddSingletonHostedService<TimeDilationPlugin>();
    }
}
