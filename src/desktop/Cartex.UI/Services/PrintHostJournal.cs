using System.Text.Json;

namespace Cartex.UI.Services;

public enum PrintHostJournalState
{
    None,
    Started,
    Completed
}

public sealed class PrintHostJournal
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<long, PrintHostJournalState>? _entries;

    public PrintHostJournal()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cartex");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "print-host-journal.json");
    }

    public async Task<PrintHostJournalState> GetStateAsync(long jobId)
    {
        await _lock.WaitAsync();
        try
        {
            await LoadAsync();
            return _entries!.GetValueOrDefault(jobId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task MarkStartedAsync(long jobId) => SetStateAsync(jobId, PrintHostJournalState.Started);
    public Task MarkCompletedAsync(long jobId) => SetStateAsync(jobId, PrintHostJournalState.Completed);

    private async Task SetStateAsync(long jobId, PrintHostJournalState state)
    {
        await _lock.WaitAsync();
        try
        {
            await LoadAsync();
            _entries![jobId] = state;
            if (_entries.Count > 5000)
                _entries = _entries.OrderByDescending(x => x.Key).Take(2500).ToDictionary();
            var temporary = _path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(_entries));
            File.Move(temporary, _path, true);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task LoadAsync()
    {
        if (_entries is not null) return;
        if (!File.Exists(_path))
        {
            _entries = [];
            return;
        }
        try
        {
            var json = await File.ReadAllTextAsync(_path);
            _entries = JsonSerializer.Deserialize<Dictionary<long, PrintHostJournalState>>(json) ?? [];
        }
        catch
        {
            try
            {
                var completed = JsonSerializer.Deserialize<HashSet<long>>(await File.ReadAllTextAsync(_path)) ?? [];
                _entries = completed.ToDictionary(x => x, _ => PrintHostJournalState.Completed);
            }
            catch
            {
                _entries = [];
            }
        }
    }
}
