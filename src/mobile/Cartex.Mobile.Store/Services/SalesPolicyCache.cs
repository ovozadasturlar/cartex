using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Settings;

namespace Cartex.Mobile.Store.Services;

// Do'kon siyosati telefonda ham kerak: navbat o'chirilgan bo'lsa ilova uni umuman
// ko'rsatmasligi kerak (NAVBAT-06). Oxirgi qiymat saqlanadi, shunda ilova internetsiz
// ochilganda ham to'g'ri ko'rinishda ishga tushadi.
public sealed class SalesPolicyCache(ISettingsApi settingsApi)
{
    private const string CacheKey = "sales_policy";
    private SalesPolicyDto? _current;
    private Task? _inFlight;
    private bool _loaded;

    // Saqlangan yozuv logout'da o'chiriladi — u yo'q bo'lsa xotiradagi nusxa boshqa
    // do'konniki, shuning uchun qaytadan yuklanadi.
    public SalesPolicyDto Current => _current is { } current && Preferences.ContainsKey(CacheKey)
        ? current
        : _current = Read();

    public Task EnsureLoadedAsync() =>
        _loaded && Preferences.ContainsKey(CacheKey)
            ? Task.CompletedTask
            : _inFlight is { IsCompleted: false } inFlight ? inFlight : _inFlight = RefreshAsync();

    public async Task<SalesPolicyDto> RefreshAsync()
    {
        try
        {
            _current = await settingsApi.GetSalesPolicyAsync();
            _loaded = true;
            Preferences.Set(CacheKey, JsonSerializer.Serialize(_current));
        }
        catch
        {
            _current = Read();
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
