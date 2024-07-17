using AssettoServer.Shared.Network.Packets.Shared;

namespace AssettoServer.Shared.Network.Packets.Outgoing;

public class CSPExplicitAdminState : IOutgoingNetworkPacket
{
    public CSPPermission Permission;

    public void ToWriter(ref PacketWriter writer)
    {
        writer.Write((byte)ACServerProtocol.Extended);
        writer.Write((ushort)CSPClientMessageType.ExplicitAdminState);
        writer.Write((ushort)Permission);
        writer.Write((ushort)0); // Currently unused buffer
    }
}

[Flags]
public enum CSPPermission : ushort
{
    None           = 0x0000, // None
    Conditions     = 0x0001, // Change time and weather
    RaceControl    = 0x0002, // Set ballast and restrictor, give out penalties
    Sessions       = 0x0004, // Restart and skip sessions
    UserModeration = 0x1000, // Kick and ban players
    Permissions    = 0x2000, // Manage permissions
    Configuration  = 0x4000, // Update the server configuration
    Admin          = 0xFFFF  // All permissions
}
