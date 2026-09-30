using AssettoServer.Server.OpenSlotFilters;
using AssettoServer.Server.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace WordFilterPlugin;

public class WordFilterModule : AssettoServerModule<WordFilterConfiguration>
{
    public override object ReferenceConfiguration => new WordFilterConfiguration
    {
        ProhibitedUsernamePatterns = ["^Player$", "^RLD!$", "^Traffic \\d+"],
        BannableChatPatterns = ["nicecar", "fallout"],
        ProhibitedChatPatterns =
        [
            "^DRIFT-STRUCTION POINTS:",
            "^ACP: App not active$",
            "^D&O Racing APP:"
        ]
    };

    public override void ConfigureDependencies(IServiceCollection services)
    {
        services.AddSingleton<IOpenSlotFilter, WordFilter>();
    }
}
