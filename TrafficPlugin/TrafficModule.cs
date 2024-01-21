using System.Numerics;
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
    public new AiParams ReferenceConfiguration = new()
    {
        CarSpecificOverrides = [
            new CarSpecificOverrides
            {
                Model = "my_car_model",
                Acceleration = 2.5f,
                Deceleration = 8.5f,
                AllowedLanes = [LaneSpawnBehavior.Left, LaneSpawnBehavior.Middle, LaneSpawnBehavior.Right],
                MaxOverbooking = 1,
                CorneringSpeedFactor = 0.5f,
                CorneringBrakeDistanceFactor = 3,
                CorneringBrakeForceFactor = 0.5f,
                EngineIdleRpm = 800,
                EngineMaxRpm = 3000,
                MaxLaneCount = 2,
                MinLaneCount = 1,
                TyreDiameterMeters = 0.8f,
                SplineHeightOffsetMeters = 0,
                VehicleLengthPostMeters = 2,
                VehicleLengthPreMeters = 2,
                MinAiSafetyDistanceMeters = 20,
                MaxAiSafetyDistanceMeters = 25,
                MinCollisionStopTimeSeconds = 0,
                MaxCollisionStopTimeSeconds = 0,
                MinSpawnProtectionTimeSeconds = 30,
                MaxSpawnProtectionTimeSeconds = 60
            }
        ],
        LaneCountSpecificOverrides = new Dictionary<int, LaneCountSpecificOverrides>
        {
            {
                1,
                new LaneCountSpecificOverrides
                {
                    MinAiSafetyDistanceMeters = 50,
                    MaxAiSafetyDistanceMeters = 100
                }
            },
            {
                2,
                new LaneCountSpecificOverrides
                {
                    MinAiSafetyDistanceMeters = 40,
                    MaxAiSafetyDistanceMeters = 80
                }
            }
        },
        IgnorePlayerObstacleSpheres = [
            new Sphere
            {
                Center = new Vector3(0, 0, 0),
                RadiusMeters = 50
            }
        ]
    };
    
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
