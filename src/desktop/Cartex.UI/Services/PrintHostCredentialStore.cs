using System.Security.Cryptography;
using System.Text;

namespace Cartex.UI.Services;

public sealed class PrintHostCredentialStore
{
    private static readonly byte[] Entropy = "Cartex.PrintHost.v1"u8.ToArray();
    private readonly string? _path;

    public PrintHostCredentialStore()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cartex");
            Directory.CreateDirectory(directory);
            _path = Path.Combine(directory, "print-host.bin");
        }
        catch
        {
            _path = null;
        }
    }

    public string? Load()
    {
        if (!OperatingSystem.IsWindows() || _path is null || !File.Exists(_path)) return null;
        try
        {
            var protectedBytes = File.ReadAllBytes(_path);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser));
        }
        catch
        {
            return null;
        }
    }

    public void Save(string token)
    {
        if (!OperatingSystem.IsWindows() || _path is null) return;
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser);
        var temporary = _path + ".tmp";
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, _path, true);
    }
}
