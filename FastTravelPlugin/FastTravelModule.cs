using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace FastTravelPlugin;

public class FastTravelModule : AssettoServerModule<FastTravelConfiguration>
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingletonHostedService<FastTravelPlugin>();
    }

    public override FastTravelConfiguration ReferenceConfiguration => new()
    {
        DisableCollisions = false,
        MapZoomValues = [100, 200, 400, 600],
        MapMoveSpeeds = [1, 2, 3, 0],
        ShowMapImage = false,
        MapFixedTargetPosition = [0, 0, 0],
        HideUntypedPoints = false,
        UseGroupDrawMode = true,
        DistanceModeRange = 100
    };
}
