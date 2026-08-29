using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Cartex.Application.Catalog;
using Cartex.Domain.Common.Exceptions;
using Cartex.Shared.Models.Catalog;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Cartex.Infrastructure.Catalog;

public sealed class CatalogPackStore : ICatalogPackStore, IDisposable
{
    private const string PublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEcXYYaCBieAFZLYgnSv1xfoQSK03u
        t9YuBkuQr5fweX5ehtYAtMWocklSQZ4jos9PSRevIboJej2kMCF2SNaZjw==
        -----END PUBLIC KEY-----
        """;

    private const string PackFile = "catalog.db";
    private const string ManifestFile = "catalog.json";
    private const string ErrorFile = "catalog.error";
    private const string StagedSuffix = ".incoming";
    private const string PreviousSuffix = ".previous";
    private const int SupportedSchemaVersion = 2;
    private const long MaxPackBytes = 512L * 1024 * 1024;
    private const long MaxManifestBytes = 8 * 1024;

    private static readonly JsonSerializerOptions ManifestFormat = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _directory;
    private readonly string _packPath;
    private readonly string _manifestPath;
    private readonly string _errorPath;
    private readonly string _previousPackPath;
    private readonly string _previousManifestPath;

    private string? _verifiedStamp;
    private CatalogPackDto? _verifiedPack;
    private string? _verifyError;

    public CatalogPackStore(IConfiguration configuration)
    {
        var configured = configuration["Catalog:PackPath"];
        _directory = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(DataRoot(), "Cartex", "catalog")
            : configured;
        _packPath = Path.Combine(_directory, PackFile);
        _manifestPath = Path.Combine(_directory, ManifestFile);
        _errorPath = Path.Combine(_directory, ErrorFile);
        _previousPackPath = _packPath + PreviousSuffix;
        _previousManifestPath = _manifestPath + PreviousSuffix;
    }

    public async Task<CatalogPackState> StateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await VerifyAsync(cancellationToken);
            return new CatalogPackState(_verifiedPack, _verifyError ?? await StoredErrorAsync(cancellationToken));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<CatalogPackDto> SaveAsync(Stream pack, Stream manifest, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        var stagedPack = _packPath + StagedSuffix;
        var stagedManifest = _manifestPath + StagedSuffix;
        try
        {
            Directory.CreateDirectory(_directory);
            RollbackInterruptedSwap();
            await StageAsync(stagedPack, pack, MaxPackBytes, cancellationToken);
            await StageAsync(stagedManifest, manifest, MaxManifestBytes, cancellationToken);

            var accepted = await InspectAsync(stagedPack, stagedManifest, cancellationToken);
            Swap(stagedPack, stagedManifest);
            File.Delete(_errorPath);
            return accepted;
        }
        catch (BusinessRuleException failure)
        {
            await RecordAsync(failure.Message, cancellationToken);
            throw;
        }
        finally
        {
            Discard(stagedPack);
            Discard(stagedManifest);
            _gate.Release();
        }
    }

    public async Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var existed = File.Exists(_packPath);
            SqliteConnection.ClearAllPools();
            File.Delete(_packPath);
            File.Delete(_manifestPath);
            File.Delete(_errorPath);
            Discard(_previousPackPath);
            Discard(_previousManifestPath);
            _verifiedStamp = null;
            _verifiedPack = null;
            _verifyError = null;
            return existed;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task<string?> VerifiedPathAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await VerifyAsync(cancellationToken);
            return _verifiedPack is null ? null : _packPath;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private void Swap(string stagedPack, string stagedManifest)
    {
        SqliteConnection.ClearAllPools();
        _verifiedStamp = null;
        try
        {
            Backup(_packPath, _previousPackPath);
            Backup(_manifestPath, _previousManifestPath);
            File.Move(stagedPack, _packPath, overwrite: true);
            File.Move(stagedManifest, _manifestPath, overwrite: true);
        }
        catch (IOException)
        {
            RollbackInterruptedSwap();
            throw new BusinessRuleException(
                "Paketni almashtirib bo'lmadi, eski paket saqlanib qoldi.", "catalog_pack_swap_failed");
        }

        Discard(_previousPackPath);
        Discard(_previousManifestPath);
    }

    private void RollbackInterruptedSwap()
    {
        if (!File.Exists(_previousPackPath) && !File.Exists(_previousManifestPath))
            return;

        SqliteConnection.ClearAllPools();
        Restore(_previousPackPath, _packPath);
        Restore(_previousManifestPath, _manifestPath);
        _verifiedStamp = null;
    }

    private static void Backup(string path, string backup)
    {
        Discard(backup);
        if (File.Exists(path))
            File.Move(path, backup);
    }

    private static void Restore(string backup, string target)
    {
        if (!File.Exists(backup))
            return;
        try
        {
            File.Move(backup, target, overwrite: true);
        }
        catch (IOException)
        {
        }
    }

    private async Task VerifyAsync(CancellationToken cancellationToken)
    {
        RollbackInterruptedSwap();
        if (!File.Exists(_packPath) || !File.Exists(_manifestPath))
        {
            _verifiedStamp = null;
            _verifiedPack = null;
            _verifyError = null;
            return;
        }

        var stamp = Stamp(_packPath) + '|' + Stamp(_manifestPath);
        if (stamp == _verifiedStamp)
            return;

        try
        {
            _verifiedPack = await InspectAsync(_packPath, _manifestPath, cancellationToken);
            _verifyError = null;
        }
        catch (BusinessRuleException failure)
        {
            _verifiedPack = null;
            _verifyError = failure.Message;
        }

        _verifiedStamp = stamp;
    }

    private static async Task<CatalogPackDto> InspectAsync(string packPath, string manifestPath, CancellationToken cancellationToken)
    {
        var manifest = await ReadManifestAsync(manifestPath, cancellationToken);
        var file = new FileInfo(packPath);
        if (file.Length != manifest.ByteSize)
            throw new BusinessRuleException(
                $"Paket hajmi manifestga mos kelmadi ({file.Length} ≠ {manifest.ByteSize} bayt).", "catalog_pack_size_mismatch");

        byte[] hash;
        await using (var content = File.OpenRead(packPath))
            hash = await SHA256.HashDataAsync(content, cancellationToken);

        if (!CryptographicOperations.FixedTimeEquals(hash, Decode(manifest.Sha256, "sha256")))
            throw new BusinessRuleException(
                "Paket fayli o'zgartirilgan: SHA-256 manifestdagi qiymatga mos kelmadi.", "catalog_pack_hash_mismatch");

        using var key = ECDsa.Create();
        key.ImportFromPem(PublicKeyPem);
        if (!key.VerifyHash(hash, Decode(manifest.Signature, "signature")))
            throw new BusinessRuleException(
                "Paket imzosi noto'g'ri: fayl bizning kalitimiz bilan imzolanmagan.", "catalog_pack_signature_invalid");

        var meta = await ReadMetaAsync(packPath, cancellationToken);
        if (!meta.TryGetValue("schemaVersion", out var schema)
            || schema != SupportedSchemaVersion.ToString(CultureInfo.InvariantCulture))
            throw new BusinessRuleException(
                $"Paket sxemasi qo'llab-quvvatlanmaydi: kutilgani {SupportedSchemaVersion}, paketda '{schema ?? "yo'q"}'.",
                "catalog_pack_schema_unsupported");

        RequireMeta(meta, "shopType", manifest.ShopType);
        RequireMeta(meta, "version", manifest.Version.ToString(CultureInfo.InvariantCulture));
        RequireMeta(meta, "rowCount", manifest.RowCount.ToString(CultureInfo.InvariantCulture));

        return new CatalogPackDto(manifest.ShopType, manifest.Version, manifest.RowCount, file.LastWriteTimeUtc);
    }

    private static void RequireMeta(Dictionary<string, string> meta, string key, string expected)
    {
        if (meta.TryGetValue(key, out var actual) && actual == expected)
            return;
        throw new BusinessRuleException(
            $"Manifest paket ichidagi ma'lumotga mos kelmadi: '{key}' manifestda '{expected}', paketda '{actual ?? "yo'q"}'.",
            "catalog_pack_meta_mismatch");
    }

    private static async Task<PackManifest> ReadManifestAsync(string path, CancellationToken cancellationToken)
    {
        PackManifest? manifest;
        try
        {
            await using var content = File.OpenRead(path);
            manifest = await JsonSerializer.DeserializeAsync<PackManifest>(content, ManifestFormat, cancellationToken);
        }
        catch (JsonException)
        {
            throw new BusinessRuleException("Manifest fayli o'qilmadi: JSON buzilgan.", "catalog_manifest_invalid");
        }

        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Sha256) || string.IsNullOrWhiteSpace(manifest.Signature))
            throw new BusinessRuleException("Manifest fayli to'liq emas: sha256 yoki imzo yo'q.", "catalog_manifest_incomplete");

        return manifest;
    }

    private static async Task<Dictionary<string, string>> ReadMetaAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqliteConnection(CatalogPackReader.ConnectionString(path));
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "select key, value from meta;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var meta = new Dictionary<string, string>(StringComparer.Ordinal);
            while (await reader.ReadAsync(cancellationToken))
                meta[reader.GetString(0)] = reader.GetString(1);
            return meta;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            throw new BusinessRuleException("Paket fayli katalog paketi emas.", "catalog_pack_unreadable");
        }
    }

    private static byte[] Decode(string value, string field)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            throw new BusinessRuleException($"Manifestdagi '{field}' base64 emas.", "catalog_manifest_invalid");
        }
    }

    private static async Task StageAsync(string path, Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await content.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            total += read;
            if (total > maxBytes)
                throw new BusinessRuleException("Yuklangan fayl ruxsat etilgan hajmdan katta.", "catalog_pack_too_large");
            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        if (total == 0)
            throw new BusinessRuleException("Yuklangan fayl bo'sh.", "catalog_pack_empty");
    }

    private async Task RecordAsync(string reason, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            await File.WriteAllTextAsync(_errorPath, reason, cancellationToken);
        }
        catch (IOException)
        {
        }
    }

    private async Task<string?> StoredErrorAsync(CancellationToken cancellationToken)
    {
        try
        {
            return File.Exists(_errorPath) ? await File.ReadAllTextAsync(_errorPath, cancellationToken) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static void Discard(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    private static string Stamp(string path)
    {
        var file = new FileInfo(path);
        return string.Create(CultureInfo.InvariantCulture, $"{file.Length}:{file.LastWriteTimeUtc.Ticks}");
    }

    private static string DataRoot()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(local) ? AppContext.BaseDirectory : local;
    }

    private sealed record PackManifest(
        string ShopType,
        int Version,
        int RowCount,
        long ByteSize,
        string Sha256,
        string Signature);
}
