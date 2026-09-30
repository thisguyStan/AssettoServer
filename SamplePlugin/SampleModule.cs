using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace SamplePlugin;

public class SampleModule : AssettoServerModule<SampleConfiguration>
{
    public override void ConfigureDependencies(IServiceCollection services)
    {
        services.AddSingletonHostedService<Sample>();
    }
}
