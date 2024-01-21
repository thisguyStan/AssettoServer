using AssettoServer.Server;
using Serilog;

namespace TrafficPlugin.Ai;

public class AiUpdater
{
    private readonly EntryCarManager _entryCarManager;

    public AiUpdater(EntryCarManager entryCarManager, 
        EntryCarAi.Factory entryCarFactory,
        ACServer server)
    {
        _entryCarManager = entryCarManager;

        for (var i = 0; i < _entryCarManager.EntryCars.Length; i++)
        {
            if (_entryCarManager.EntryCars[i].AiMode != AiMode.None)
            {
                // TODO this is fucky
                _entryCarManager.EntryCars[i] = entryCarFactory(_entryCarManager.EntryCars[i].Model,
                    _entryCarManager.EntryCars[i].Skin, 
                    _entryCarManager.EntryCars[i].SessionId);
            }
        }
        
        server.Update += OnUpdate;
    }
    
    private void OnUpdate(object sender, EventArgs args)
    {
        for (var i = 0; i < _entryCarManager.EntryCars.Length; i++)
        {
            var entryCar = _entryCarManager.EntryCars[i];
            if (entryCar.AiControlled)
            {
                // TODO this is fucky
                if (!entryCar.GetType().IsAssignableTo(typeof(EntryCarAi)))
                {
                    Log.Error("Couldn't cast EntryCar to EntryCarAI in OnUpdate");
                }
                ((EntryCarAi)entryCar).AiUpdate();
            }
        }
    }
}
