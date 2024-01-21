using AssettoServer.Server.Plugin;
using Autofac;
using Microsoft.Extensions.Hosting;

namespace DynamicTrafficPlugin;

public class DynamicTrafficModule : AssettoServerModule<DynamicTrafficConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<DynamicTrafficDensity>().As<IHostedService>().SingleInstance();
    }
}
