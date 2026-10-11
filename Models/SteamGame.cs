namespace ModFrame.Models;

internal sealed record SteamGame(string AppId, string Name, string LaunchOptions)
{
    public string DisplayName => $"{Name}  •  App ID {AppId}";
}

internal sealed record SteamLibrarySession(
    string RemoteConfigPath,
    string AccountId,
    IReadOnlyList<SteamGame> Games);

internal enum LaunchOptionAction
{
    Set,
    Remove
}
