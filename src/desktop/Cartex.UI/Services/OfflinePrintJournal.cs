using System.Text.Json;
using Cartex.Shared.Models.Printing;

namespace Cartex.UI.Services;

public sealed record OfflinePrintRecord(
    long BranchId,
    PrintJobKind Kind,
    string SourceType,
    string SourceId,
    string PayloadJson,
    int Copies,
    bool IsReprint,
    string? Reason,
    string IdempotencyKey,
    DateTime PrintedAtUtc);

/// Jobs printed on this machine while the server was unreachable wait here and are
/// pushed to the server as completed history once the connection returns.
public sealed class OfflinePrintJournal
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<OfflinePrintRecord>? _records;

    public OfflinePrintJournal()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cartex");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "offline-print-journal.json");
    }

    public async Task AddAsync(OfflinePrintRecord record)
    {
        await _lock.WaitAsync();
        try
        {
            await LoadAsync();
            _records!.Add(record);
            await SaveAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<OfflinePrintRecord>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await LoadAsync();
            return [.. _records!];
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveAsync(string idempotencyKey)
    {
        await _lock.WaitAsync();
        try
        {
            await LoadAsync();
            if (_records!.RemoveAll(x => x.IdempotencyKey == idempotencyKey) > 0)
                await SaveAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task LoadAsync()
    {
        if (_records is not null) return;
        try
        {
            _records = File.Exists(_path)
                ? JsonSerializer.Deserialize<List<OfflinePrintRecord>>(await File.ReadAllTextAsync(_path)) ?? []
                : [];
        }
        catch
        {
            _records = [];
        }
    }

    private async Task SaveAsync()
    {
        var temporary = _path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(_records));
        File.Move(temporary, _path, true);
    }
}
