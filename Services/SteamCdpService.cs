using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ModFrame.Services;

internal sealed class SteamCdpService(AdbService adb)
{
    private readonly AdbService _adb = adb;

    public async Task SetLaunchOptionsAsync(
        string serial,
        string appId,
        string launchOptions,
        CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(appId, out var numericAppId))
        {
            throw new InvalidOperationException("The selected game has an invalid Steam app ID.");
        }

        var errors = new List<string>();
        foreach (var remotePort in new[] { 8080, 8081 })
        {
            var localPort = 0;
            try
            {
                localPort = await _adb.CreatePortForwardAsync(serial, remotePort, cancellationToken);
                var targetUrl = await FindSharedContextAsync(localPort, cancellationToken);
                if (targetUrl is null)
                {
                    errors.Add($"Steam DevTools port {remotePort} did not expose SharedJSContext.");
                    continue;
                }

                await EvaluateLaunchOptionChangeAsync(
                    targetUrl,
                    numericAppId,
                    launchOptions,
                    cancellationToken);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                errors.Add(exception.Message);
            }
            finally
            {
                if (localPort > 0)
                {
                    try
                    {
                        await _adb.RemovePortForwardAsync(serial, localPort, CancellationToken.None);
                    }
                    catch
                    {
                        // ADB removes stale forwards automatically when the device disconnects.
                    }
                }
            }
        }

        throw new InvalidOperationException(
            "Could not reach the running Steam client over USB. Keep Steam open and enable CEF Remote Debugging in the Steam Frame developer settings. " +
            string.Join(" ", errors.Distinct()));
    }

    private static async Task<Uri?> FindSharedContextAsync(
        int localPort,
        CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var json = await client.GetStringAsync(
            $"http://127.0.0.1:{localPort}/json/list",
            cancellationToken);
        using var document = JsonDocument.Parse(json);

        foreach (var target in document.RootElement.EnumerateArray())
        {
            var title = ReadString(target, "title");
            if (!title.Contains("SharedJSContext", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var advertisedUrl = ReadString(target, "webSocketDebuggerUrl");
            if (!Uri.TryCreate(advertisedUrl, UriKind.Absolute, out var parsedUrl))
            {
                continue;
            }

            var localUrl = new UriBuilder(parsedUrl)
            {
                Host = "127.0.0.1",
                Port = localPort
            };
            return localUrl.Uri;
        }

        return null;
    }

    private static async Task EvaluateLaunchOptionChangeAsync(
        Uri webSocketUrl,
        int appId,
        string launchOptions,
        CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(webSocketUrl, cancellationToken);

        var expectedJson = JsonSerializer.Serialize(launchOptions);
        var expression = $$"""
            (async () => {
                const api = window.SteamClient?.Apps;
                if (!api || typeof api.SetAppLaunchOptions !== "function") {
                    throw new Error("SteamClient.Apps.SetAppLaunchOptions is unavailable.");
                }
                if (typeof api.RegisterForAppDetails !== "function") {
                    throw new Error("SteamClient.Apps.RegisterForAppDetails is unavailable.");
                }

                const expected = {{expectedJson}};
                return await new Promise((resolve) => {
                    let registration;
                    let timer;
                    let lastValue = "";
                    let armed = false;
                    let settled = false;

                    const finish = (ok) => {
                        if (settled) return;
                        settled = true;
                        if (timer) clearTimeout(timer);
                        registration?.unregister?.();
                        resolve({ ok, actual: lastValue });
                    };

                    registration = api.RegisterForAppDetails({{appId}}, (details) => {
                        lastValue = details?.strLaunchOptions ?? "";
                        if (armed && lastValue === expected) finish(true);
                    });

                    armed = true;
                    api.SetAppLaunchOptions({{appId}}, expected);
                    if (!settled) timer = setTimeout(() => finish(false), 6000);
                });
            })()
            """;

        var request = JsonSerializer.Serialize(new
        {
            id = 1,
            method = "Runtime.evaluate",
            @params = new
            {
                expression,
                awaitPromise = true,
                returnByValue = true
            }
        });
        var requestBytes = Encoding.UTF8.GetBytes(request);
        await socket.SendAsync(
            requestBytes,
            WebSocketMessageType.Text,
            true,
            cancellationToken);

        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);

        while (true)
        {
            var response = await ReceiveMessageAsync(socket, linkedSource.Token);
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            if (!root.TryGetProperty("id", out var id) || id.GetInt32() != 1)
            {
                continue;
            }

            if (root.TryGetProperty("error", out var protocolError))
            {
                throw new InvalidOperationException(
                    $"Steam rejected the DevTools request: {protocolError}");
            }

            var result = root.GetProperty("result");
            if (result.TryGetProperty("exceptionDetails", out var exceptionDetails))
            {
                var description = exceptionDetails.TryGetProperty("exception", out var exception) &&
                                  exception.TryGetProperty("description", out var exceptionDescription)
                    ? exceptionDescription.GetString()
                    : exceptionDetails.ToString();
                throw new InvalidOperationException(
                    $"Steam could not change the launch options: {description}");
            }

            var remoteResult = result.GetProperty("result");
            if (!remoteResult.TryGetProperty("value", out var value) ||
                !value.TryGetProperty("ok", out var ok) ||
                !ok.GetBoolean())
            {
                var actual = value.ValueKind == JsonValueKind.Object &&
                             value.TryGetProperty("actual", out var actualValue)
                    ? actualValue.GetString()
                    : null;
                throw new InvalidOperationException(
                    $"Steam did not confirm the launch-option change. Current value: {actual ?? "unknown"}");
            }

            return;
        }
    }

    private static async Task<string> ReceiveMessageAsync(
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new InvalidOperationException("Steam closed the DevTools connection.");
            }

            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }
}
