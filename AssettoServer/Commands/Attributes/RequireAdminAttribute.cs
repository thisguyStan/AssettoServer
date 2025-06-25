using System.Linq;
using Qmmands;
using System.Threading.Tasks;
using AssettoServer.Commands.Contexts;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.UserGroup;
using AssettoServer.Shared.Network.Packets.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace AssettoServer.Commands.Attributes;

public class RequireAdminAttribute : CheckAttribute
{
    public CSPPermission Permission { get; set; } = CSPPermission.Admin;
    public override async ValueTask<CheckResult> CheckAsync(CommandContext context)
    {
        switch (context)
        {
            case BaseCommandContext { IsAdministrator: true }:
                return CheckResult.Successful;
            case ChatCommandContext chatContext:
            {
                var config = chatContext.Services.GetRequiredService<ACServerConfiguration>();
                var userGroupManager = chatContext.Services.GetRequiredService<UserGroupManager>();

                if (config.Extra.UserGroupCommandPermissions != null)
                {
                    foreach (var perm in config.Extra.UserGroupCommandPermissions)
                    {
                        if ((perm.Commands.Contains(chatContext.Command.Name)
                            || (perm.CSPPermissions.Aggregate(CSPPermission.None, (current, next) => current | next) & Permission) == Permission)
                            && userGroupManager.TryResolve(perm.UserGroup, out var group)
                            && await group.ContainsAsync(chatContext.Client.Guid))
                        {
                            return CheckResult.Successful;
                        }
                    }
                }
                goto default;
            }
            default:
                return CheckResult.Failed("You are not an administrator.");
        }
    }
}
