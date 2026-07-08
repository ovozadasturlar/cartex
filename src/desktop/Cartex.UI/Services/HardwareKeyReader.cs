using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Cartex.UI.Services;

public sealed record DetectedDrive(string Root, string Serial, string? KeyFile, string? KeyContent);

public static class HardwareKeyReader
{
    public static DetectedDrive? ScanForKey()
    {
        foreach (var drive in EnumerateRemovable())
        {
            var keyFile = SafeFindKey(drive.Root);
            if (keyFile is null) continue;
            try
            {
                return drive with { KeyFile = keyFile, KeyContent = File.ReadAllText(keyFile).Trim() };
            }
            catch (IOException) { }
        }
        return null;
    }

    public static DetectedDrive? ScanForBlankDrive()
    {
        foreach (var drive in EnumerateRemovable())
            return drive;
        return null;
    }

    private static IEnumerable<DetectedDrive> EnumerateRemovable()
    {
        if (!OperatingSystem.IsWindows()) yield break;

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Removable || !drive.IsReady) continue;
            var serial = VolumeSerial(drive.RootDirectory.FullName);
            if (serial is null) continue;
            yield return new DetectedDrive(drive.RootDirectory.FullName, serial, null, null);
        }
    }

    private static string? SafeFindKey(string root)
    {
        try { return Directory.EnumerateFiles(root, "*.key").FirstOrDefault(); }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
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
