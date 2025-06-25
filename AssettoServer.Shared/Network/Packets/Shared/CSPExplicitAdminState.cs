using AssettoServer.Shared.Network.Packets.Outgoing;

namespace AssettoServer.Shared.Network.Packets.Shared;

public class CSPExplicitAdminState : CSPClientMessageOutgoing
{
    public CSPPermission Permission;

    public CSPExplicitAdminState()
    {
        Type = CSPClientMessageType.ExplicitAdminState;
    }

    protected override void ToWriter(BinaryWriter writer)
    {
        writer.Write((ushort)Permission);
        writer.Write((ushort)1); // Currently unused buffer
    }
}

/// <summary>
/// except for None and Admin these are just ideas and basically unused
/// </summary>
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
