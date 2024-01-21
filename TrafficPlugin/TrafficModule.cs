using AssettoServer.Server.OpenSlotFilters;
using AssettoServer.Server.Plugin;
using Autofac;
using TrafficPlugin.Ai;
using TrafficPlugin.Ai.OpenSlotFilters;
using TrafficPlugin.Ai.Splines;
using TrafficPlugin.Configuration;

namespace TrafficPlugin;

public class TrafficModule : AssettoServerModule<AiParams>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AiParamsFixer>().AsSelf().SingleInstance();
        
        builder.RegisterType<AiState>().AsSelf();

        builder.RegisterType<AiBehavior>().AsSelf().As<IAssettoServerAutostart>().SingleInstance();
        builder.RegisterType<AiUpdater>().AsSelf().SingleInstance().AutoActivate();
        builder.RegisterType<AiSlotFilter>().As<IOpenSlotFilter>();
        
        builder.RegisterType<AiSplineWriter>().AsSelf();
        builder.RegisterType<FastLaneParser>().AsSelf();
        builder.RegisterType<AiSplineLocator>().AsSelf();
        builder.Register((AiSplineLocator locator) => locator.Locate()).AsSelf().SingleInstance();
    }
}
