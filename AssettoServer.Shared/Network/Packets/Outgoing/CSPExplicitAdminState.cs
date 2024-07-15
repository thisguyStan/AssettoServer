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
    Conditions     = 0x0000_0001, // Change time and weather
    RaceControl    = 0x0000_0002, // Set ballast and restrictor, give out penalties
    Sessions       = 0x0000_0004, // Restart and skip sessions
    UserModeration = 0x1000_0000, // Kick and ban players
    Permissions    = 0x2000_0000, // Manage permissions
    Admin          = 0xFFFF_FFFF  // All permissions
}
