﻿using AssettoServer.Commands;
using AssettoServer.Commands.Attributes;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using JetBrains.Annotations;
using Qmmands;

namespace TrafficPlugin;

[RequireAdmin]
[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public class TrafficCommandModule : ACModuleBase
{
    private readonly ACServerConfiguration _configuration;
    private readonly EntryCarManager _entryCarManager;
    
    public TrafficCommandModule(ACServerConfiguration configuration, EntryCarManager entryCarManager)
    {
        _configuration = configuration;
        _entryCarManager = entryCarManager;
    }

    [Command("setaioverbooking")]
    public void SetAiOverbooking(int count)
    {
        foreach (var aiCar in _entryCarManager.EntryCars.Where(car => car.AiControlled && car.Client == null))
        {
            aiCar.SetAiOverbooking(count);
        }
        Reply($"AI overbooking set to {count}");
    }
}
