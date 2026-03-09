using System.IO;
using System.Text.Json;

namespace Cartex.UI.Services;

public sealed class SettingsService
{
    private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());
    public static SettingsService Instance => _instance.Value;

    private readonly string _settingsPath;
    private SettingsData _data;

    private SettingsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "Cartex");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
        _data = Load();
    }

    public string Theme
    {
        get => _data.Theme;
        set { _data.Theme = value; Save(); }
    }

    public string Language
    {
        get => _data.Language;
        set { _data.Language = value; Save(); }
    }

    public bool IsTouchMode
    {
        get => _data.IsTouchMode;
        set { _data.IsTouchMode = value; Save(); }
    }

    public string ApiBaseUrl
    {
        get => _data.ApiBaseUrl;
        set { _data.ApiBaseUrl = value; Save(); }
    }

    private SettingsData Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
            }
        }
        catch { }

        return new SettingsData();
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }

    private sealed class SettingsData
    {
        public string Theme { get; set; } = "Light";
        public string Language { get; set; } = "en";
        public bool IsTouchMode { get; set; }
        public string ApiBaseUrl { get; set; } = "http://localhost:5015";
    }
}
