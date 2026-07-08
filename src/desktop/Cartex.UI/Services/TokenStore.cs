using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Cartex.UI.Services;

public sealed record TokenBundle(string Access, string Refresh);

public interface ITokenStore
{
    void Save(TokenBundle bundle);
    TokenBundle? Load();
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

    public void Save(TokenBundle bundle)
    {
        if (_path is null || !OperatingSystem.IsWindows()) return;
        try
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(bundle);
            var data = ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_path, data);
        }
        catch
        {
        }
    }

    public TokenBundle? Load()
    {
        if (_path is null || !OperatingSystem.IsWindows() || !File.Exists(_path)) return null;
        try
        {
            var data = ProtectedData.Unprotect(File.ReadAllBytes(_path), Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<TokenBundle>(data);
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
