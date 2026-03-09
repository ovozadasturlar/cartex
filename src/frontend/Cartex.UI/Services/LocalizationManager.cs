using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace Cartex.UI.Services;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationManager> _instance = new(() => new LocalizationManager());
    public static LocalizationManager Instance => _instance.Value;

    private Dictionary<string, string> _strings = new();
    private string _currentLanguage = "en";
    private readonly Dictionary<string, Dictionary<string, string>> _cache = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocalizationManager()
    {
        LoadLanguage(_currentLanguage);
    }

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage == value) return;
            _currentLanguage = value;
            LoadLanguage(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }

    public string this[string key] =>
        _strings.TryGetValue(key, out var value) ? value : $"[{key}]";

    public static string[] AvailableLanguages => ["en", "ru", "uz-latn", "uz-cyrl"];

    public static string GetLanguageDisplayName(string code) => code switch
    {
        "en" => "English",
        "ru" => "Русский",
        "uz-latn" => "O'zbek (Lotin)",
        "uz-cyrl" => "Ўзбек (Кирилл)",
        _ => code
    };

    public void LoadLanguage(string lang)
    {
        if (_cache.TryGetValue(lang, out var cached))
        {
            _strings = cached;
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"Cartex.UI.Assets.Languages.{lang}.json";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            _strings = new Dictionary<string, string>();
            return;
        }

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        _cache[lang] = _strings;
    }
}
