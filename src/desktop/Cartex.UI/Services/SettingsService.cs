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

    public AppLanguage Language
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

    public bool RememberMe
    {
        get => _data.RememberMe;
        set { _data.RememberMe = value; Save(); }
    }

    public double PosCartWidth
    {
        get => _data.PosCartWidth;
        set { _data.PosCartWidth = value; Save(); }
    }

    public string DeviceId
    {
        get
        {
            if (string.IsNullOrEmpty(_data.DeviceId))
            {
                _data.DeviceId = Guid.NewGuid().ToString("N");
                Save();
            }
            return _data.DeviceId;
        }
    }

    public bool OfflineCacheEnabled
    {
        get => _data.OfflineCacheEnabled;
        set { _data.OfflineCacheEnabled = value; Save(); }
    }

    public long OfflineWarehouseId
    {
        get => _data.OfflineWarehouseId;
        set { _data.OfflineWarehouseId = value; Save(); }
    }

    public bool SettingsSidebarCollapsed
    {
        get => _data.SettingsSidebarCollapsed;
        set { _data.SettingsSidebarCollapsed = value; Save(); }
    }

    public bool PosListMode
    {
        get => _data.PosListMode;
        set { _data.PosListMode = value; Save(); }
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
        public AppLanguage Language { get; set; } = AppLanguage.En;
        public AppMode Mode { get; set; }
        public string ApiBaseUrl { get; set; } = "http://localhost:5015";
        public bool RememberMe { get; set; }
        public double PosCartWidth { get; set; } = 430;
        public string? DeviceId { get; set; }
        public bool OfflineCacheEnabled { get; set; }
        public long OfflineWarehouseId { get; set; }
        public bool SettingsSidebarCollapsed { get; set; }
        public bool PosListMode { get; set; }
    }
}
