using System;
using System.Linq;
using AssettoServer.Server.Configuration;
using AssettoServer.Shared.Model;

namespace TrafficAiPlugin;

public class EntryCarTrafficAiFactory : IEntryCarFactory
{
    public string ClientType => "TRAFFICAI";

    private readonly EntryCarTrafficAi.Factory _entryCarFactory;
    private readonly ACServerConfiguration _configuration;

    public EntryCarTrafficAiFactory(EntryCarTrafficAi.Factory entryCarFactory, ACServerConfiguration configuration)
    {
        _entryCarFactory = entryCarFactory;
        _configuration = configuration;
    }
    
    public IEntryCar Create(IEntry entry, byte sessionId)
    {
        var car = _entryCarFactory(entry.Model, entry.Skin, sessionId);
        
        var driverOptions = CSPDriverOptions.Parse(entry.Skin);
        car.SpectatorMode = entry.SpectatorMode;
        car.Ballast = entry.Ballast;
        car.Restrictor = entry.Restrictor;
        car.FixedSetup = entry.FixedSetup;
        car.DriverOptionsFlags = driverOptions;
        car.AiMode = AiMode.None;
        car.AiControlled = false;
        car.NetworkDistanceSquared = MathF.Pow(_configuration.Extra.NetworkBubbleDistance, 2);
        car.OutsideNetworkBubbleUpdateRateMs = 1000 / _configuration.Extra.OutsideNetworkBubbleRefreshRateHz;
        car.LegalTyres = entry.LegalTyres ?? _configuration.Server.LegalTyres;
        if (!string.IsNullOrWhiteSpace(entry.Guid))
        {
            car.AllowedGuids = entry.Guid.Split(';').Select(ulong.Parse).ToList();
        }
        
        return car;
    }
}
