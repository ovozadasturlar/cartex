using System.Security.Cryptography;
using System.Text;

namespace Cartex.UI.Services;

/// The host credential belongs to one server. Keeping a single file meant switching the
/// app between servers overwrote the other one's token, and that server then refused
/// every registration for good, so each server gets its own file.
public sealed class PrintHostCredentialStore
{
    private static readonly byte[] Entropy = "Cartex.PrintHost.v1"u8.ToArray();
    private readonly string? _directory;

    public PrintHostCredentialStore()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cartex");
            Directory.CreateDirectory(_directory);
        }
        catch
        {
            _directory = null;
        }
    }

    private string? Path_ => _directory is null
        ? null
        : Path.Combine(_directory, $"print-host-{ServerKey()}.bin");

    private static string ServerKey()
    {
        var server = SettingsService.Instance.ApiBaseUrl.Trim().TrimEnd('/').ToLowerInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(server)))[..16].ToLowerInvariant();
    }

    public string? Load()
    {
        if (!OperatingSystem.IsWindows() || Path_ is not { } path || !File.Exists(path)) return null;
        try
        {
            var protectedBytes = File.ReadAllBytes(path);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser));
        }
        catch
        {
            return null;
        }
    }

    public void Save(string token)
    {
        if (!OperatingSystem.IsWindows() || Path_ is not { } path) return;
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser);
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path, true);
    }
}
