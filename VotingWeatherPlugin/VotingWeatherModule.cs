using AssettoServer.Server.Plugin;
using Autofac;

namespace VotingWeatherPlugin;

public class VotingWeatherModule : AssettoServerModule<VotingWeatherConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<VotingTime>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
        builder.RegisterType<VotingWeather>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
    }
}
