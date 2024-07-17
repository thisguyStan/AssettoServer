using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssettoServer.Commands.Attributes;
using AssettoServer.Shared.Network.Http.Responses;
using DotNext;
using Microsoft.AspNetCore.Mvc;
using Qmmands;

namespace AssettoServer.Network.Http;

[ApiController]
public class DiscoveryController : ControllerBase
{
    private readonly CommandService _commandService;
    private readonly DiscoveryCache _cache;

    public DiscoveryController(CommandService commandService, DiscoveryCache cache)
    {
        _commandService = commandService;
        _cache = cache;
    }

    [HttpGet("/discovery/commands")]
    public async Task<CommandResponse> GetCommands()
    {
        if (_cache.Commands != null)
            return _cache.Commands;
            
        var response = new CommandResponse();

        foreach (var command in _commandService.GetAllCommands())
        {
            var admin = command.Checks.FirstOrDefault(x => x.GetType() == typeof(RequireAdminAttribute)) as RequireAdminAttribute;
            var parentAdmin = command.Module.Checks.FirstOrDefault(x => x.GetType() == typeof(RequireAdminAttribute)) as RequireAdminAttribute;
            var description = command.Description;
            var args = command.Parameters.ToDictionary(x => x.Name, x => x.Type.Name);

            response.Commands.Add(new CommandItem
            {
                Command = command.Name,
                Arguments = args.Count > 0 ? args : null,
                RequiredPermission = admin?.Permission ?? parentAdmin?.Permission ?? 0,
                Description = string.IsNullOrEmpty(description) ? null : description
            });
        }

        return _cache.Commands = response;
    }
}
