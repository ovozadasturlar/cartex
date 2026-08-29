using System.Text.Json;

namespace Cartex.UI.Services;

public enum PrintHostJournalState
{
    None,
    Started,
    Completed
}

/// Chop etish boshlangach jarayon o'lib qolsa, xuddi shu ish qayta kelganda ikkinchi marta
/// qog'oz chiqarmaslik uchun jurnal. Kalit hujjatning o'ziga bog'lanadi (id:tur:manba) —
/// yalang'och ish raqami bo'lsa, baza qayta tiklanganda yangi ishlar eski raqamlarga to'g'ri
/// kelib, chop etilmasdan "bajarildi" deb yuborilardi.
public sealed class PrintHostJournal
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, PrintHostJournalState>? _entries;

    public PrintHostJournal()
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cartex");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "print-host-journal.json");
    }

    public async Task<PrintHostJournalState> GetStateAsync(string key)
    {
        await _lock.WaitAsync();
        try
        {
            await LoadAsync();
            return _entries!.GetValueOrDefault(key);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task MarkStartedAsync(string key) => SetStateAsync(key, PrintHostJournalState.Started);
    public Task MarkCompletedAsync(string key) => SetStateAsync(key, PrintHostJournalState.Completed);

    private async Task SetStateAsync(string key, PrintHostJournalState state)
    {
        await _lock.WaitAsync();
        try
        {
            await LoadAsync();
            _entries![key] = state;
            if (_entries.Count > 5000)
                _entries = _entries.Skip(_entries.Count - 2500).ToDictionary();
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
            var raw = JsonSerializer.Deserialize<Dictionary<string, PrintHostJournalState>>(json) ?? [];
            // Eski format kalitlari (yalang'och raqamlar) boshqa bazaning ishlariga to'g'ri
            // kelib qolmasligi uchun yuk paytida tashlab yuboriladi.
            _entries = raw.Where(x => x.Key.Contains(':')).ToDictionary();
        }
        catch
        {
            _entries = [];
        }
    }
}
