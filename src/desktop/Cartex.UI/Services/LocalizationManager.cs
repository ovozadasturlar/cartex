using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Cartex.UI.Models;

namespace Cartex.UI.Services;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationManager> _instance = new(() => new LocalizationManager());
    public static LocalizationManager Instance => _instance.Value;

    private Dictionary<string, string> _strings = new();
    private AppLanguage _currentLanguage = AppLanguage.En;
    private readonly Dictionary<AppLanguage, Dictionary<string, string>> _cache = new();
    private readonly List<WeakReference<Action>> _weakHandlers = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action LanguageChanged
    {
        add => _weakHandlers.Add(new WeakReference<Action>(value));
        remove { }
    }

    private LocalizationManager()
    {
        LoadLanguage(_currentLanguage);
    }

    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage == value) return;
            _currentLanguage = value;
            LoadLanguage(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            NotifyWeakHandlers();
        }
    }

    private void NotifyWeakHandlers()
    {
        for (int i = _weakHandlers.Count - 1; i >= 0; i--)
        {
            if (_weakHandlers[i].TryGetTarget(out var handler))
                handler();
            else
                _weakHandlers.RemoveAt(i);
        }
    }

    public string this[string key] =>
        _strings.TryGetValue(key, out var value) ? value : $"[{key}]";

    public string? Find(string key) => _strings.TryGetValue(key, out var value) ? value : null;

    public static AppLanguage[] AvailableLanguages => [AppLanguage.En, AppLanguage.Ru, AppLanguage.UzLatn, AppLanguage.UzCyrl];

    public static string GetLanguageDisplayName(AppLanguage lang) => lang switch
    {
        AppLanguage.En => "English",
        AppLanguage.Ru => "Русский",
        AppLanguage.UzLatn => "O'zbek (Lotin)",
        AppLanguage.UzCyrl => "Ўзбек (Кирилл)",
        _ => lang.ToString()
    };

    public static string GetLanguageShortCode(AppLanguage lang) => lang switch
    {
        AppLanguage.En => "EN",
        AppLanguage.Ru => "RU",
        AppLanguage.UzLatn => "UZ",
        AppLanguage.UzCyrl => "ЎЗ",
        _ => "??"
    };

    private static string GetLanguageCode(AppLanguage lang) => lang switch
    {
        AppLanguage.En => "en",
        AppLanguage.Ru => "ru",
        AppLanguage.UzLatn => "uz-latn",
        AppLanguage.UzCyrl => "uz-cyrl",
        _ => "en"
    };

    public void LoadLanguage(AppLanguage lang)
    {
        if (_cache.TryGetValue(lang, out var cached))
        {
            _strings = cached;
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"Cartex.UI.Assets.Languages.{GetLanguageCode(lang)}.json";

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
