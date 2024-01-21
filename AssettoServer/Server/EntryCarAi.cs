namespace AssettoServer.Server;

public enum AiMode
{
    None,
    Auto,
    Fixed
}

public partial class EntryCar
{
    public bool AiControlled { get; set; }
    public AiMode AiMode { get; set; }
    public string? AiName { get; set; }
    public bool AiEnableColorChanges { get; set; } = false;
    public byte[] AiPakSequenceIds { get; }
    public byte[] LastSeenAiSpawn { get; }
    
    public virtual void SetAiOverbooking(int count){ }
}
