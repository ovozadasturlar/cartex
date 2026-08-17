using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Settings;

namespace Cartex.Mobile.Store.Services;

/// Do'kon siyosati telefonda ham kerak: navbat o'chirilgan bo'lsa ilova uni umuman
/// ko'rsatmasligi kerak (NAVBAT-06). Oxirgi qiymat saqlanadi, shunda ilova internetsiz
/// ochilganda ham to'g'ri ko'rinishda ishga tushadi.
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
