using System.Diagnostics.CodeAnalysis;
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
        get => NormalizeLoopback(_data.ApiBaseUrl);
        set { _data.ApiBaseUrl = NormalizeLoopback(value); Save(); }
    }

    private static string NormalizeLoopback(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "http://127.0.0.1:5015";
        return url.Replace("://localhost", "://127.0.0.1", StringComparison.OrdinalIgnoreCase);
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

    private string? _machineDeviceId;

    /// The identity must survive reinstalls and profile changes, so it is derived from the
    /// machine instead of being stored in a file. MachineGuid alone is not enough because
    /// cloned Windows images share it - the machine name is mixed in.
    public string DeviceId
    {
        get
        {
            if (_machineDeviceId is not null) return _machineDeviceId;
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                    if (key?.GetValue("MachineGuid") is string machineGuid && machineGuid.Length > 0)
                    {
                        var bytes = System.Security.Cryptography.SHA256.HashData(
                            System.Text.Encoding.UTF8.GetBytes($"{machineGuid}|{Environment.MachineName}"));
                        return _machineDeviceId = Convert.ToHexString(bytes)[..32].ToLowerInvariant();
                    }
                }
                catch { }
            }
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

    public bool HubEnabled
    {
        get => _data.HubEnabled;
        set { _data.HubEnabled = value; Save(); }
    }

    public bool OfflineAllowSales
    {
        get => _data.OfflineAllowSales;
        set { _data.OfflineAllowSales = value; Save(); }
    }

    public bool OfflineAllowPayments
    {
        get => _data.OfflineAllowPayments;
        set { _data.OfflineAllowPayments = value; Save(); }
    }

    public bool OfflineAllowSupplies
    {
        get => _data.OfflineAllowSupplies;
        set { _data.OfflineAllowSupplies = value; Save(); }
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

    public List<string> EnabledFeatures
    {
        get => _data.EnabledFeatures ??= ["loyalty", "reports", "stock_transfers", "supplies", "suppliers", "accounts", "partners"];
        set { _data.EnabledFeatures = value; Save(); }
    }

    [SuppressMessage("Meziantou.Analyzer", "MA0045",
        Justification = "Konstruktordan chaqiriladi; kichik lokal sozlama fayli.")]
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

    [SuppressMessage("Meziantou.Analyzer", "MA0045",
        Justification = "Chaqiruvchilar property setter'lar; kichik lokal sozlama fayli.")]
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
        public string ApiBaseUrl { get; set; } = "http://127.0.0.1:5015";
        public bool RememberMe { get; set; }
        public double PosCartWidth { get; set; } = 430;
        public string? DeviceId { get; set; }
        public bool OfflineCacheEnabled { get; set; }
        public long OfflineWarehouseId { get; set; }
        public bool OfflineAllowSales { get; set; } = true;
        public bool OfflineAllowPayments { get; set; } = true;
        public bool OfflineAllowSupplies { get; set; } = true;
        public bool HubEnabled { get; set; }
        public bool SettingsSidebarCollapsed { get; set; }
        public bool PosListMode { get; set; }
        public List<string> EnabledFeatures { get; set; } = ["loyalty", "reports", "stock_transfers", "supplies", "suppliers", "accounts", "partners"];
    }
}
