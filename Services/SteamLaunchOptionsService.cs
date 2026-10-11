using ModFrame.Models;

namespace ModFrame.Services;

internal sealed class SteamLaunchOptionsService(AdbService adb)
{
    private readonly AdbService _adb = adb;

    public async Task<SteamLibrarySession> LoadLibraryAsync(
        string serial,
        CancellationToken cancellationToken = default)
    {
        var configResult = await _adb.RunDeviceAsync(
            serial,
            [
                "shell",
                "find /home/steamos/.local/share/Steam/userdata -type f -path '*/config/localconfig.vdf' " +
                "-printf '%T@\\t%p\\n' 2>/dev/null | sort -rn | head -n 1 | cut -f2-"
            ],
            cancellationToken,
            20_000);

        var remoteConfigPath = configResult.Output.Trim();
        if (!configResult.Success || string.IsNullOrWhiteSpace(remoteConfigPath))
        {
            throw new InvalidOperationException(
                "No Steam user configuration was found. Sign in to Steam on the headset first.");
        }

        var manifestCommand = """
            find /home/steamos/.local/share/Steam/steamapps /run/media -maxdepth 7 -type f -name 'appmanifest_*.acf' -exec awk -F'"' 'BEGIN{id="";name=""} /"appid"/ && id=="" {id=$4} /"name"/ && name=="" {name=$4} END {if(id!="") printf "%s\t%s\n",id,name}' {} \; 2>/dev/null
            """;
        var gamesResult = await _adb.RunDeviceAsync(
            serial,
            ["shell", manifestCommand.Trim()],
            cancellationToken,
            30_000);
        if (!gamesResult.Success)
        {
            throw new InvalidOperationException("Could not read the installed Steam games.");
        }

        var configText = await DownloadTextAsync(serial, remoteConfigPath, cancellationToken);
        var editor = new VdfLaunchOptionsEditor(configText);
        var games = ParseGames(gamesResult.Output)
            .Select(game => game with { LaunchOptions = editor.GetLaunchOptions(game.AppId) })
            .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (games.Count == 0)
        {
            throw new InvalidOperationException("No installed Steam games were found on the headset.");
        }

        return new SteamLibrarySession(
            remoteConfigPath,
            ReadAccountId(remoteConfigPath),
            games);
    }

    private async Task<string> DownloadTextAsync(
        string serial,
        string remotePath,
        CancellationToken cancellationToken)
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "ModFrame",
            "ReadConfig",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var localPath = Path.Combine(temporaryDirectory, "localconfig.vdf");
            await _adb.DownloadAsync(serial, remotePath, localPath, cancellationToken);
            return await File.ReadAllTextAsync(localPath, cancellationToken);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }
    }

    private static List<SteamGame> ParseGames(string output)
    {
        var games = new Dictionary<string, SteamGame>(StringComparer.Ordinal);
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 2);
            if (parts.Length != 2 || !parts[0].All(char.IsDigit))
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(parts[1]) ? $"Steam app {parts[0]}" : parts[1].Trim();
            games[parts[0]] = new SteamGame(parts[0], name, string.Empty);
        }

        return games.Values.ToList();
    }

    private static string ReadAccountId(string configPath)
    {
        const string marker = "/userdata/";
        var start = configPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return "Unknown";
        }

        start += marker.Length;
        var end = configPath.IndexOf('/', start);
        return end < 0 ? configPath[start..] : configPath[start..end];
    }

}
