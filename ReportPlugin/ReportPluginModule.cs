using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace ReportPlugin;

public class ReportPluginModule : AssettoServerModule<ReportConfiguration>
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingletonHostedService<ReportPlugin>();
    }
}
