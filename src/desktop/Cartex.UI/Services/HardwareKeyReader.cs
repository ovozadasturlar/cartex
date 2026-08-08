using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Cartex.UI.Services;

public sealed record DetectedDrive(string Root, string Serial);

public sealed record DetectedKey(string Root, string Serial, string File, string Content, string? Username);

public static class HardwareKeyReader
{
    public static List<DetectedKey> ScanForKeys()
    {
        var keys = new List<DetectedKey>();
        foreach (var drive in EnumerateRemovable())
        {
            foreach (var file in SafeFindKeys(drive.Root))
            {
                try
                {
                    var content = File.ReadAllText(file).Trim();
                    keys.Add(new DetectedKey(drive.Root, drive.Serial, file, content, TryReadUsername(content)));
                }
                catch (IOException) { }
            }
        }
        return keys;
    }

    public static int CountKeys(string root) => SafeFindKeys(root).Count();

    private static string? TryReadUsername(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(Convert.FromBase64String(content.Split('.')[0]));
            return doc.RootElement.GetProperty("U").GetString();
        }
        catch { return null; }
    }

    public static List<DetectedDrive> ScanForDrives() => [.. EnumerateRemovable()];

    private static IEnumerable<DetectedDrive> EnumerateRemovable()
    {
        if (!OperatingSystem.IsWindows()) yield break;

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Removable || !drive.IsReady) continue;
            var serial = VolumeSerial(drive.RootDirectory.FullName);
            if (serial is null) continue;
            yield return new DetectedDrive(drive.RootDirectory.FullName, serial);
        }
    }

    private static IEnumerable<string> SafeFindKeys(string root)
    {
        try { return Directory.EnumerateFiles(root, "cartex-*.key").ToList(); }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    private static string? VolumeSerial(string root)
    {
        if (GetVolumeInformation(root, null, 0, out var serial, out _, out _, null, 0))
            return serial.ToString("X8");
        return null;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetVolumeInformation(
        string rootPathName, StringBuilder? volumeNameBuffer, int volumeNameSize,
        out uint volumeSerialNumber, out uint maxComponentLength, out uint fileSystemFlags,
        StringBuilder? fileSystemNameBuffer, int fileSystemNameSize);
}
