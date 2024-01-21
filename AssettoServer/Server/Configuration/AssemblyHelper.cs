namespace AssettoServer.Server.Configuration;

public static class AssemblyHelper
{
    public static string GetAssemblyInformationalVersion() {
        return ThisAssembly.AssemblyInformationalVersion;
    }
}
