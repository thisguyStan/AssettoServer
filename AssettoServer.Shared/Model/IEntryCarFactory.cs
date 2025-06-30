namespace AssettoServer.Shared.Model;

public interface IEntryCarFactory
{
    public string ClientType { get; }

    public IEntryCar Create(IEntry entry, byte sessionId);
}
