using System.Collections.Generic;
using AssettoServer.Shared.Network.Packets.Outgoing;
using JetBrains.Annotations;

namespace AssettoServer.Server.Configuration.Extra;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class UserGroupCommandPermissions
{
    public required string UserGroup { get; set; }
    public required List<string> Commands { get; set; }
    public required List<CSPPermission> CSPPermissions { get; set; }
}
