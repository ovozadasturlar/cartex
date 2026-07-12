using System.ComponentModel;
using System.Text.Json;

namespace Cartex.Mobile.Core;

public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private Dictionary<string, string> _strings = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language { get; private set; } = "uz-latn";

    public string this[string key] => _strings.TryGetValue(key, out var value) ? value : key;

    public Task InitAsync() => LoadAsync(Preferences.Get("app_lang", "uz-latn"));

    public async Task SetLanguageAsync(string code)
    {
        Preferences.Set("app_lang", code);
        await LoadAsync(code);
    }

    private async Task LoadAsync(string code)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync($"i18n/{code}.json").ConfigureAwait(false);
        _strings = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream).ConfigureAwait(false) ?? [];
        Language = code;
        if (MainThread.IsMainThread) Raise();
        else MainThread.BeginInvokeOnMainThread(Raise);
    }

    private void Raise()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
