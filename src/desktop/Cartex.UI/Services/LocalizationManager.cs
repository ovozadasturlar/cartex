using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
        add
        {
            if (_weakHandlers.Count > 256)
                _weakHandlers.RemoveAll(w => !w.TryGetTarget(out _));
            _weakHandlers.Add(new WeakReference<Action>(value));
        }
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

    /// Sana tanlagichdagi oy nomlari va hafta kunlari matn fayldan emas, .NET madaniyatidan
    /// keladi. Madaniyat til bilan birga o'zgarmasa, o'zbekcha ekranda "July" chiqib qoladi.
    private static CultureInfo GetCulture(AppLanguage lang) => CultureInfo.GetCultureInfo(lang switch
    {
        AppLanguage.Ru => "ru-RU",
        AppLanguage.UzLatn => "uz-Latn-UZ",
        AppLanguage.UzCyrl => "uz-Cyrl-UZ",
        _ => "en-US"
    });

    private static void ApplyCulture(AppLanguage lang)
    {
        var culture = GetCulture(lang);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public void LoadLanguage(AppLanguage lang)
    {
        ApplyCulture(lang);
        if (_cache.TryGetValue(lang, out var cached))
        {
            _strings = cached;
            return;
        }

        _strings = LoadTable(lang);
        _cache[lang] = _strings;
    }

    [SuppressMessage("Meziantou.Analyzer", "MA0045",
        Justification = "Assembly ichiga joylangan resurs — disk I/O emas; chaqiruvchilar sinxron indekser va Get.")]
    private static Dictionary<string, string> LoadTable(AppLanguage lang)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"Cartex.UI.Assets.Languages.{GetLanguageCode(lang)}.json";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return new Dictionary<string, string>();
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
    }

    /// Hujjat tili interfeys tilidan farq qilishi mumkin (masalan, Z-hisobot) — jadval
    /// madaniyatni almashtirmasdan olinadi.
    public string Get(string? languageCode, string key)
    {
        var lang = languageCode switch
        {
            "en" => AppLanguage.En,
            "ru" => AppLanguage.Ru,
            "uz-latn" => AppLanguage.UzLatn,
            "uz-cyrl" => AppLanguage.UzCyrl,
            _ => _currentLanguage
        };
        if (lang == _currentLanguage) return this[key];
        if (!_cache.TryGetValue(lang, out var table))
        {
            table = LoadTable(lang);
            _cache[lang] = table;
        }
        return table.TryGetValue(key, out var value) ? value : $"[{key}]";
    }
}
