namespace ModFrame.Models;

internal enum FrameConnectionState
{
    Disconnected,
    Unauthorized,
    Offline,
    Connected,
    AdbMissing,
    Error
}

internal sealed record FrameDevice(
    FrameConnectionState State,
    string? Serial = null,
    string? HostName = null,
    string? Message = null);
