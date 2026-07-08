using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Cartex.UI.Services;

public interface ITokenStore
{
    void Save(string token);
    string? Load();
    void Clear();
}

public sealed class TokenStore : ITokenStore
{
    private static readonly byte[] Entropy = "Cartex.Auth.v1"u8.ToArray();
    private readonly string? _path;

    public TokenStore()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cartex");
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "auth.bin");
        }
        catch
        {
            _path = null;
        }
    }

    public void Save(string token)
    {
        if (_path is null || !OperatingSystem.IsWindows()) return;
        try
        {
            var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_path, data);
        }
        catch
        {
        }
    }

    public string? Load()
    {
        if (_path is null || !OperatingSystem.IsWindows() || !File.Exists(_path)) return null;
        try
        {
            var data = ProtectedData.Unprotect(File.ReadAllBytes(_path), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            return null;
        }
    }

    public void Clear()
    {
        if (_path is null) return;
        try
        {
            if (File.Exists(_path)) File.Delete(_path);
        }
        catch
        {
        }
    }
}
