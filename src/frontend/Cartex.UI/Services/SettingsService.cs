using System.IO;
using System.Text.Json;
using Cartex.UI.Models;

namespace Cartex.UI.Services;

public sealed class SettingsService
{
    private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());
    public static SettingsService Instance => _instance.Value;

    private readonly string? _settingsPath;
    private SettingsData _data;

    private SettingsService()
    {
        if (!OperatingSystem.IsBrowser())
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dir = Path.Combine(appData, "Cartex");
                Directory.CreateDirectory(dir);
                _settingsPath = Path.Combine(dir, "settings.json");
            }
            catch
            {
                _settingsPath = null;
            }
        }

        _data = Load();
    }

    public AppTheme Theme
    {
        get => _data.Theme;
        set { _data.Theme = value; Save(); }
    }

    public string Language
    {
        get => _data.Language;
        set { _data.Language = value; Save(); }
    }

    public AppMode Mode
    {
        get => _data.Mode;
        set { _data.Mode = value; Save(); }
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
            if (_settingsPath is not null && File.Exists(_settingsPath))
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
        if (_settingsPath is null) return;
        try
        {
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }

    private sealed class SettingsData
    {
        public AppTheme Theme { get; set; } = AppTheme.Light;
        public string Language { get; set; } = "en";
        public AppMode Mode { get; set; }
        public string ApiBaseUrl { get; set; } = "http://localhost:5015";
    }
}
