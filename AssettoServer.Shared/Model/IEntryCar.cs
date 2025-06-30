using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Shared.Model;

// TODO AI BROKEN :)))))

public interface IEntryCar
{
    public byte SessionId { get; }
    public bool HasUpdateToSend { get; set; }
    public ushort Ping { get; }
    public int TimeOffset { get; }
    public CarStatus Status { get; }
    public string Model { get; }
    public string Skin { get; }
    public float Ballast { get; }
    public int Restrictor { get; }
    public bool IsSpectator { get; }
    public List<ulong> AllowedGuids { get; }
    public bool EnableCollisions { get; }
    public bool AiControlled { get; }
    public string? AiName { get; }
    public AiMode AiMode { get; }
    public DriverOptionsFlags DriverOptionsFlags { get; }
    public bool GetPositionUpdateForCar(IEntryCar<IClient> toCar, out PositionUpdateOut positionUpdateOut);
    
}

public interface IEntryCar<out TClient> : IEntryCar where TClient : IClient
{
    public long LastPingTime { get; set; }
    public long LastPongTime { get; }
    public TClient? Client { get; }
    public IEntryCar? TargetCar { get; }
    public long LastActiveTime { get; }
}

[Flags]
public enum DriverOptionsFlags
{
    AllowColorChange = 0x10,
    AllowTeleporting = 0x20
}
