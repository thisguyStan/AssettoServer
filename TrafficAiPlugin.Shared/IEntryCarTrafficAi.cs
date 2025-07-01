using AssettoServer.Server;
using AssettoServer.Shared.Model;

namespace TrafficAiPlugin.Shared;

public interface IEntryCarTrafficAi : IEntryCar
{
    public IAiState?[] LastSeenAiState { get; }
    
    public void SetAiOverbooking(int count);
    public bool TryResetPosition();
    public void AiUpdate();
}
