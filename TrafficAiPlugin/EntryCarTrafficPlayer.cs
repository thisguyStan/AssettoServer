using AssettoServer.Server;
using TrafficAiPlugin.Splines;

namespace TrafficAiPlugin;

public class EntryCarTrafficPlayer
{
    private readonly EntryCar _entryCar;
    private readonly SessionManager _sessionManager;
    private readonly AiSpline _aiSpline;

    public EntryCarTrafficPlayer(EntryCar entryCar, SessionManager sessionManager, AiSpline aiSpline)
    {
        _entryCar = entryCar;
        _sessionManager = sessionManager;
        _aiSpline = aiSpline;
    }

    public bool TryResetPosition()
    {
        if (_sessionManager.ServerTimeMilliseconds < _sessionManager.CurrentSession.StartTimeMilliseconds + 20_000 
            || (_sessionManager.ServerTimeMilliseconds > _sessionManager.CurrentSession.EndTimeMilliseconds
                && _sessionManager.CurrentSession.EndTimeMilliseconds > 0))
            return false;
        
        _entryCar.SetCollisions(false);
        
        _ = Task.Run(async () =>
        {
            await Task.Delay(250);
            
            var (splinePointId, _) = _aiSpline.WorldToSpline(_entryCar.Status.Position);

            var splinePoint = _aiSpline.Points[splinePointId];
        
            var position = splinePoint.Position;
            var direction = - _aiSpline.Operations.GetForwardVector(splinePoint.NextId);
            
            _entryCar.Client?.SendTeleportCarPacket(position, direction);
            await Task.Delay(10000);
            _entryCar.SetCollisions(true);
        });
    
        _entryCar.Logger.Information("Reset position for {Player} ({SessionId})",_entryCar.Client?.Name, _entryCar.Client?.SessionId);
        return true;
    }
}
