using System.Text.Json.Serialization;
using AssettoServer.Shared.Network.Packets.Shared;

namespace AssettoServer.Shared.Network.Http.Responses;

public class CommandResponse
{
    public List<CommandItem> Commands { get; set; } = [];
}

public class CommandItem
{
    public required string Command { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Arguments { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
    public CSPPermission RequiredPermission { get; set; }
}
