namespace ModFrame.Services;

internal sealed record AdbCommandResult(int ExitCode, string Output, string Error)
{
    public bool Success => ExitCode == 0;
}
