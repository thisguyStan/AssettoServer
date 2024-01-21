using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.OpenSlotFilters;
using TrafficPlugin.Configuration;

namespace TrafficPlugin.Ai.OpenSlotFilters;

public class AiSlotFilter : OpenSlotFilterBase
{
    private readonly EntryCarManager _entryCarManager;
    private readonly ACServerConfiguration _configuration;
    private readonly AiParams _aiParams;

    public AiSlotFilter(EntryCarManager entryCarManager, AiParams aiParams, ACServerConfiguration configuration)
    {
        _entryCarManager = entryCarManager;
        _aiParams = aiParams;
        _configuration = configuration;
    }

    public override bool IsSlotOpen(EntryCar entryCar, ulong guid)
    {
        if (entryCar.AiMode == AiMode.Fixed
            || (_aiParams.MaxPlayerCount > 0 && _entryCarManager.ConnectedCars.Count >= _aiParams.MaxPlayerCount))
        {
            return false;
        }
        
        return base.IsSlotOpen(entryCar, guid);
    }
}
