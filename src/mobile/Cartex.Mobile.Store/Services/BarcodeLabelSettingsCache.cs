using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Settings;

namespace Cartex.Mobile.Store.Services;

public sealed class BarcodeLabelSettingsCache(ISettingsApi settingsApi)
{
    private const string CacheKey = "barcode_label_settings";
    private BarcodeLabelSettingsDto? _current;

    public BarcodeLabelSettingsDto Current => _current ??= Read();

    public async Task<BarcodeLabelSettingsDto> RefreshAsync()
    {
        try
        {
            _current = await settingsApi.GetBarcodeLabelAsync();
            Preferences.Set(CacheKey, JsonSerializer.Serialize(_current));
        }
        catch
        {
            _current ??= Read();
        }
        return _current;
    }

    private static BarcodeLabelSettingsDto Read()
    {
        try
        {
            var json = Preferences.Get(CacheKey, "");
            return string.IsNullOrWhiteSpace(json)
                ? new BarcodeLabelSettingsDto()
                : JsonSerializer.Deserialize<BarcodeLabelSettingsDto>(json) ?? new BarcodeLabelSettingsDto();
        }
        catch
        {
            return new BarcodeLabelSettingsDto();
        }
    }
}
