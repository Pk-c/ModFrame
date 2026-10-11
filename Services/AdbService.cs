using System.Diagnostics;
using System.Globalization;
using System.Text;
using ModFrame.Models;

namespace ModFrame.Services;

internal sealed class AdbService
{
    private readonly string? _adbPath;

    public AdbService()
    {
        _adbPath = LocateAdb();
    }

    public bool IsAvailable => _adbPath is not null;

    public string AdbLocation => _adbPath ?? "Not found";

    public async Task<FrameDevice> DetectFrameAsync(CancellationToken cancellationToken = default)
    {
        if (_adbPath is null)
        {
            return new FrameDevice(
                FrameConnectionState.AdbMissing,
                Message: "ADB is missing from the application folder.");
        }

        try
        {
            var devicesResult = await RunAdbAsync(["devices", "-l"], cancellationToken, 12_000);
            if (!devicesResult.Success)
            {
                return new FrameDevice(
                    FrameConnectionState.Error,
                    Message: CleanError(devicesResult));
            }

            var devices = ParseDevices(devicesResult.Output);
            var unauthorized = devices.FirstOrDefault(device => device.State == "unauthorized");
            if (!string.IsNullOrWhiteSpace(unauthorized.Serial))
            {
                return new FrameDevice(
                    FrameConnectionState.Unauthorized,
                    unauthorized.Serial,
                    Message: "Unlock the headset and approve USB debugging.");
            }

            var offline = devices.FirstOrDefault(device => device.State == "offline");
            var onlineDevices = devices.Where(device => device.State == "device").ToList();

            foreach (var device in onlineDevices)
            {
                var probe = await RunDeviceAsync(
                    device.Serial,
                    ["shell", "printf 'HOST='; hostname; printf 'OS='; sed -n 's/^ID=//p' /etc/os-release | tr -d '\"' | head -n 1"],
                    cancellationToken,
                    8_000);

                var hostName = ReadValue(probe.Output, "HOST=");
                var osId = ReadValue(probe.Output, "OS=");
                if (probe.Success &&
                    (osId.Equals("steamos", StringComparison.OrdinalIgnoreCase) ||
                     hostName.Contains("frame", StringComparison.OrdinalIgnoreCase)))
                {
                    return new FrameDevice(
                        FrameConnectionState.Connected,
                        device.Serial,
                        string.IsNullOrWhiteSpace(hostName) ? "Steam Frame" : hostName,
                        "Connected over USB");
                }
            }

            if (!string.IsNullOrWhiteSpace(offline.Serial))
            {
                return new FrameDevice(
                    FrameConnectionState.Offline,
                    offline.Serial,
                    Message: "The USB device is offline. Reconnect the cable.");
            }

            if (onlineDevices.Count > 0)
            {
                return new FrameDevice(
                    FrameConnectionState.Disconnected,
                    Message: "An ADB device was found, but it is not a Steam Frame.");
            }

            return new FrameDevice(
                FrameConnectionState.Disconnected,
                Message: "Connect your Steam Frame with a USB data cable.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new FrameDevice(FrameConnectionState.Error, Message: exception.Message);
        }
    }

    public async Task<IReadOnlyList<RemoteEntry>> ListDirectoryAsync(
        string serial,
        string path,
        CancellationToken cancellationToken = default)
    {
        var command = $"find {ShellQuote(path)} -mindepth 1 -maxdepth 1 -printf '%y\\t%s\\t%T@\\t%f\\0'";
        var result = await RunDeviceAsync(serial, ["shell", command], cancellationToken, 20_000);
        EnsureSuccess(result, "Could not open this folder");

        var entries = new List<RemoteEntry>();
        foreach (var record in result.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = record.Split('\t', 4);
            if (fields.Length != 4 || fields[3] is "." or "..")
            {
                continue;
            }

            _ = long.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size);
            _ = double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var epochSeconds);
            var modified = epochSeconds > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds((long)(epochSeconds * 1000))
                : DateTimeOffset.MinValue;
            var name = fields[3];

            entries.Add(new RemoteEntry(
                name,
                CombineRemotePath(path, name),
                fields[0] == "d",
                size,
                modified));
        }

        return entries
            .OrderByDescending(entry => entry.IsDirectory)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task UploadFileAsync(
        string serial,
        string localPath,
        string remoteDirectory,
        CancellationToken cancellationToken = default)
    {
        var result = await RunDeviceAsync(
            serial,
            ["push", localPath, EnsureTrailingSlash(remoteDirectory)],
            cancellationToken,
            0);
        EnsureSuccess(result, $"Could not upload {Path.GetFileName(localPath)}");
    }

    public async Task UploadFileToPathAsync(
        string serial,
        string localPath,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        var result = await RunDeviceAsync(
            serial,
            ["push", localPath, remotePath],
            cancellationToken,
            0);
        EnsureSuccess(result, $"Could not upload {Path.GetFileName(localPath)}");
    }

    public async Task UploadDirectoryAsync(
        string serial,
        string localDirectory,
        string remoteDirectory,
        CancellationToken cancellationToken = default)
    {
        var folderName = new DirectoryInfo(localDirectory).Name;
        var target = CombineRemotePath(remoteDirectory, folderName);
        await CreateDirectoryAsync(serial, target, cancellationToken);

        var source = Path.Combine(localDirectory, ".");
        var result = await RunDeviceAsync(serial, ["push", source, EnsureTrailingSlash(target)], cancellationToken, 0);
        EnsureSuccess(result, $"Could not upload {folderName}");
    }

    public async Task DownloadAsync(
        string serial,
        string remotePath,
        string localPath,
        CancellationToken cancellationToken = default)
    {
        var result = await RunDeviceAsync(serial, ["pull", remotePath, localPath], cancellationToken, 0);
        EnsureSuccess(result, $"Could not download {GetRemoteName(remotePath)}");
    }

    public async Task CreateDirectoryAsync(
        string serial,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        var result = await RunDeviceAsync(
            serial,
            ["shell", $"mkdir -p -- {ShellQuote(remotePath)}"],
            cancellationToken,
            15_000);
        EnsureSuccess(result, "Could not create the folder");
    }

    public async Task DeleteAsync(
        string serial,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        var result = await RunDeviceAsync(
            serial,
            ["shell", $"rm -rf -- {ShellQuote(remotePath)}"],
            cancellationToken,
            0);
        EnsureSuccess(result, $"Could not delete {GetRemoteName(remotePath)}");
    }

    public async Task RenameAsync(
        string serial,
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var result = await RunDeviceAsync(
            serial,
            ["shell", $"mv -- {ShellQuote(sourcePath)} {ShellQuote(destinationPath)}"],
            cancellationToken,
            20_000);
        EnsureSuccess(result, "Could not rename the item");
    }

    public async Task<AdbCommandResult> RunDeviceAsync(
        string serial,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default,
        int timeoutMilliseconds = 30_000)
    {
        var allArguments = new List<string> { "-s", serial };
        allArguments.AddRange(arguments);
        return await RunAdbAsync(allArguments, cancellationToken, timeoutMilliseconds);
    }

    public async Task<int> CreatePortForwardAsync(
        string serial,
        int remotePort,
        CancellationToken cancellationToken = default)
    {
        var result = await RunDeviceAsync(
            serial,
            ["forward", "tcp:0", $"tcp:{remotePort}"],
            cancellationToken,
            10_000);
        EnsureSuccess(result, "Could not create the USB connection to Steam");

        if (!int.TryParse(result.Output.Trim(), out var localPort))
        {
            throw new InvalidOperationException("ADB did not return a valid forwarded port.");
        }

        return localPort;
    }

    public async Task RemovePortForwardAsync(
        string serial,
        int localPort,
        CancellationToken cancellationToken = default)
    {
        await RunDeviceAsync(
            serial,
            ["forward", "--remove", $"tcp:{localPort}"],
            cancellationToken,
            10_000);
    }

    public static string CombineRemotePath(string directory, string name)
    {
        if (directory == "/")
        {
            return "/" + name.TrimStart('/');
        }

        return directory.TrimEnd('/') + "/" + name.TrimStart('/');
    }

    public static string GetParentPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return "/";
        }

        var trimmed = path.TrimEnd('/');
        var index = trimmed.LastIndexOf('/');
        return index <= 0 ? "/" : trimmed[..index];
    }

    public static string GetRemoteName(string path)
    {
        if (path == "/")
        {
            return "/";
        }

        var trimmed = path.TrimEnd('/');
        var index = trimmed.LastIndexOf('/');
        return index < 0 ? trimmed : trimmed[(index + 1)..];
    }

    private async Task<AdbCommandResult> RunAdbAsync(
        IEnumerable<string> arguments,
        CancellationToken cancellationToken,
        int timeoutMilliseconds)
    {
        if (_adbPath is null)
        {
            return new AdbCommandResult(-1, string.Empty, "ADB executable not found.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _adbPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutSource = timeoutMilliseconds > 0
            ? new CancellationTokenSource(timeoutMilliseconds)
            : new CancellationTokenSource();
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException("ADB did not respond in time.");
        }
        catch
        {
            TryKill(process);
            throw;
        }

        return new AdbCommandResult(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }

    private static List<(string Serial, string State)> ParseDevices(string output)
    {
        var devices = new List<(string Serial, string State)>();
        foreach (var rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith('*'))
            {
                continue;
            }

            var parts = line.Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                devices.Add((parts[0], parts[1]));
            }
        }

        return devices;
    }

    private static string ReadValue(string output, string prefix)
    {
        var index = output.IndexOf(prefix, StringComparison.Ordinal);
        if (index < 0)
        {
            return string.Empty;
        }

        var start = index + prefix.Length;
        var end = output.IndexOfAny(['\r', '\n'], start);
        return (end < 0 ? output[start..] : output[start..end]).Trim();
    }

    internal static string ShellQuote(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'") + "'";
    }

    private static string EnsureTrailingSlash(string path)
    {
        return path.EndsWith('/') ? path : path + "/";
    }

    private static void EnsureSuccess(AdbCommandResult result, string context)
    {
        if (result.Success)
        {
            return;
        }

        var detail = CleanError(result);
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
            ? context
            : $"{context}. {detail}");
    }

    private static string CleanError(AdbCommandResult result)
    {
        var text = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
        return text.Trim().Replace("\r", " ").Replace("\n", " ");
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch
        {
            // The process may have exited between the state check and the kill request.
        }
    }

    private static string? LocateAdb()
    {
        var bundledCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", "Tools", "ADB", "adb.exe"),
            Path.Combine(AppContext.BaseDirectory, "tools", "platform-tools", "adb.exe"),
            Path.Combine(AppContext.BaseDirectory, "tools", "adb.exe")
        };

        var bundled = bundledCandidates.FirstOrDefault(File.Exists);
        if (bundled is not null)
        {
            return bundled;
        }

        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return pathVariable
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => Path.Combine(path.Trim(), "adb.exe"))
            .FirstOrDefault(File.Exists);
    }
}
