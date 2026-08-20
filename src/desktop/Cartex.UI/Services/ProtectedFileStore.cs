using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Cartex.UI.Services;

/// <summary>
/// Keeps a device secret outside settings.json. Windows uses DPAPI CurrentUser. Unix uses
/// AES-GCM with a per-user key protected by 0600 permissions; this protects backups and
/// accidental disclosure while retaining restart-safe offline operation.
/// </summary>
public sealed class ProtectedFileStore
{
    private readonly byte[] _entropy;
    private readonly string? _path;
    private readonly string? _keyPath;

    public ProtectedFileStore(string fileName, string keyFileName, string entropy)
    {
        _entropy = Encoding.UTF8.GetBytes(entropy);
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cartex");
            Directory.CreateDirectory(directory);
            _path = Path.Combine(directory, fileName);
            _keyPath = Path.Combine(directory, keyFileName);
        }
        catch
        {
            _path = null;
            _keyPath = null;
        }
    }

    public byte[]? Read()
    {
        if (_path is null || !File.Exists(_path)) return null;
        try
        {
            var protectedBytes = File.ReadAllBytes(_path);
            return OperatingSystem.IsWindows()
                ? ProtectedData.Unprotect(protectedBytes, _entropy, DataProtectionScope.CurrentUser)
                : DecryptUnix(protectedBytes);
        }
        catch
        {
            return null;
        }
    }

    public void Write(byte[] plain)
    {
        if (_path is null) throw new InvalidOperationException("Maxfiy ma'lumotni saqlash jildi mavjud emas.");
        var protectedBytes = OperatingSystem.IsWindows()
            ? ProtectedData.Protect(plain, _entropy, DataProtectionScope.CurrentUser)
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
        aes.Encrypt(nonce, plain, cipher, tag, _entropy);
        return [.. nonce, .. tag, .. cipher];
    }

    private byte[] DecryptUnix(byte[] encrypted)
    {
        if (encrypted.Length < 28) throw new CryptographicException("Protected payload is invalid.");
        var key = LoadOrCreateUnixKey();
        var plain = new byte[encrypted.Length - 28];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(encrypted.AsSpan(0, 12), encrypted.AsSpan(28), encrypted.AsSpan(12, 16), plain, _entropy);
        return plain;
    }

    [SuppressMessage("Meziantou.Analyzer", "MA0045",
        Justification = "Sinxron kripto zanjiri ichida 32 baytlik kalit fayli; async qiymat qo'shmaydi.")]
    private byte[] LoadOrCreateUnixKey()
    {
        if (_keyPath is null) throw new InvalidOperationException("Protected key path is missing.");
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
