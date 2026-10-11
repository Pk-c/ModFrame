using System.Globalization;

namespace ModFrame.Models;

internal sealed record RemoteEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    long Size,
    DateTimeOffset Modified)
{
    public string TypeText => IsDirectory ? "Folder" : "File";

    public string SizeText => IsDirectory ? "—" : FormatSize(Size);

    public string ModifiedText => Modified == DateTimeOffset.MinValue
        ? "—"
        : Modified.ToLocalTime().ToString("yyyy-MM-dd  HH:mm", CultureInfo.InvariantCulture);

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes} {units[unitIndex]}"
            : $"{value:0.##} {units[unitIndex]}";
    }
}
