using AssettoServer.Shared.Network.Packets.Outgoing;
using Serilog;

namespace AssettoServer.Shared.Model;

public interface IClient
{
    public byte SessionId { get; }
    public ulong Guid { get; }
    public string HashedGuid { get; }
    public string? Name { get; }
    public string? Team { get; }
    public string? NationCode { get; }
    public bool HasUdpEndpoint { get; }
    public bool IsConnected { get; }
    public bool HasSentFirstUpdate { get; }
    public int? CSPVersion { get; }
    public bool SupportsCSPCustomUpdate { get; }
    public ILogger Logger { get; }

    public void SendPacket<TPacket>(TPacket packet) where TPacket : IOutgoingNetworkPacket;
    public void SendPacketUdp<TPacket>(in TPacket packet) where TPacket : IOutgoingNetworkPacket;
    public Task DisconnectAsync();
}

public interface IMultiConnectionClient : IClient
{
    
}
