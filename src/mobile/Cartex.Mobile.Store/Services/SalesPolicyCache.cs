using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Settings;

namespace Cartex.Mobile.Store.Services;

/// The till has to know the shop's policy before it can draw the right buttons, and it has to
/// keep knowing it when the phone is offline — so the last answer is kept on the device.
public sealed class SalesPolicyCache(ISettingsApi settingsApi)
{
    private const string CacheKey = "sales_policy";
    private SalesPolicyDto? _current;

    public SalesPolicyDto Current => _current ??= Read();

    public async Task<SalesPolicyDto> RefreshAsync()
    {
        try
        {
            _current = await settingsApi.GetSalesPolicyAsync();
            Preferences.Set(CacheKey, JsonSerializer.Serialize(_current));
        }
        catch
        {
            _current ??= Read();
        }
        return _current;
    }

    private static SalesPolicyDto Read()
    {
        try
        {
            var json = Preferences.Get(CacheKey, "");
            return string.IsNullOrWhiteSpace(json)
                ? new SalesPolicyDto()
                : JsonSerializer.Deserialize<SalesPolicyDto>(json) ?? new SalesPolicyDto();
        }
        catch
        {
            return new SalesPolicyDto();
        }
    }
}
