using System.Security.Cryptography;
using System.Text.Json;

namespace Cartex.UI.Services;

public sealed record OfflineLeaseCredential(
    string DeviceId,
    long LeaseId,
    long WarehouseId,
    long Epoch,
    string Token,
    long LastAcceptedSequence);

/// <summary>
/// Keeps the bearer-like offline lease token outside settings.json. Windows uses
/// DPAPI CurrentUser. Unix uses AES-GCM with a per-user key protected by 0600
/// permissions; this protects backups and accidental disclosure while retaining
/// restart-safe offline operation.
/// </summary>
public sealed class OfflineLeaseCredentialStore
{
    private static readonly byte[] Entropy = "Cartex.OfflineAuthority.v2"u8.ToArray();
    private readonly string? _path;
    private readonly string? _keyPath;

    public OfflineLeaseCredentialStore()
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cartex");
            Directory.CreateDirectory(directory);
            _path = Path.Combine(directory, "offline-authority.bin");
            _keyPath = Path.Combine(directory, "offline-authority.key");
        }
        catch
        {
            _path = null;
            _keyPath = null;
        }
    }

    public OfflineLeaseCredential? Load()
    {
        if (_path is null || !File.Exists(_path)) return null;
        try
        {
            var protectedBytes = File.ReadAllBytes(_path);
            var plain = OperatingSystem.IsWindows()
                ? ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser)
                : DecryptUnix(protectedBytes);
            return JsonSerializer.Deserialize<OfflineLeaseCredential>(plain);
        }
        catch
        {
            return null;
        }
    }

    public void Save(OfflineLeaseCredential credential)
    {
        if (_path is null) throw new InvalidOperationException("Oflayn vakolatni xavfsiz saqlash jildi mavjud emas.");
        var plain = JsonSerializer.SerializeToUtf8Bytes(credential);
        var protectedBytes = OperatingSystem.IsWindows()
            ? ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser)
            : EncryptUnix(plain);
        var temporary = _path + ".tmp";
        File.WriteAllBytes(temporary, protectedBytes);
        ProtectUnixFile(temporary);
        File.Move(temporary, _path, true);
        ProtectUnixFile(_path);
    }

    public void Clear()
    {
        if (_path is null || !File.Exists(_path)) return;
        try { File.Delete(_path); } catch { }
    }

    private byte[] EncryptUnix(byte[] plain)
    {
        var key = LoadOrCreateUnixKey();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plain, cipher, tag, Entropy);
        return [.. nonce, .. tag, .. cipher];
    }

    private byte[] DecryptUnix(byte[] encrypted)
    {
        if (encrypted.Length < 28) throw new CryptographicException("Credential payload is invalid.");
        var key = LoadOrCreateUnixKey();
        var plain = new byte[encrypted.Length - 28];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(encrypted.AsSpan(0, 12), encrypted.AsSpan(28), encrypted.AsSpan(12, 16), plain, Entropy);
        return plain;
    }

    private byte[] LoadOrCreateUnixKey()
    {
        if (_keyPath is null) throw new InvalidOperationException("Credential key path is missing.");
        if (File.Exists(_keyPath)) return File.ReadAllBytes(_keyPath);
        var key = RandomNumberGenerator.GetBytes(32);
        var temporary = _keyPath + ".tmp";
        File.WriteAllBytes(temporary, key);
        ProtectUnixFile(temporary);
        try { File.Move(temporary, _keyPath, false); }
        catch (IOException)
        {
            try { File.Delete(temporary); } catch { }
            return File.ReadAllBytes(_keyPath);
        }
        ProtectUnixFile(_keyPath);
        return key;
    }

    private static void ProtectUnixFile(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        catch { }
    }
}
