using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;
using VotingPresetPlugin.Preset;

namespace VotingPresetPlugin;

public class VotingPresetModule : AssettoServerModule<VotingPresetConfiguration>
{
    public override void ConfigureDependencies(IServiceCollection services)
    {
        services.AddSingleton<PresetConfigurationManager>();
        services.AddSingleton<PresetManager>();
        services.AddSingletonHostedService<VotingPresetPlugin>();
    }

    public override VotingPresetConfiguration ReferenceConfiguration => new()
    {
        EnableReconnect = true,
        EnableVote = false,
        EnableStayOnTrack = false,
        IntervalMinutes = 60,
        TransitionDelaySeconds = 30,
        TransitionDurationSeconds = 10,
        Meta = new()
        {
            Name = "SRP",
            AdminOnly = false
        }
    };
}
