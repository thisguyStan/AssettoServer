using AssettoServer.Shared.Network.Packets.Shared;

namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class CSPExplicitAdminState : IOutgoingNetworkPacket
{
    public CSPPermission Permission;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.Extended);
        writer.Write((ushort)CSPClientMessageType.ExplicitAdminState);
        writer.Write((uint)Permission);
    }
}

[Flags]
public enum CSPPermission : uint
{
    Conditions = 1 << 0,
    RaceControl = 1 << 1,
    Sessions = 1 << 2,
    UserModeration = 1 << 3,
    FullAdmin = uint.MaxValue, 
}
