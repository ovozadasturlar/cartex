using System.Text.Json;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using Cartex.Shared.Models.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class CustomerViewModel(AgentDb db) : ObservableObject, IQueryAttributable
{
    [ObservableProperty] private string _fullName = "";
    [ObservableProperty] private string? _phone;
    [ObservableProperty] private string? _address;
    [ObservableProperty] private string _debtText = "";
    [ObservableProperty] private string? _limitText;
    [ObservableProperty] private string _staleText = "";
    [ObservableProperty] private bool _hasDebt;

    private long _customerId;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("customerId", out var id))
            _customerId = (long)id;
    }

    public async Task AppearAsync()
    {
        var c = await db.GetCustomerAsync(_customerId);
        if (c is null) return;
        var currency = await db.GetMetaAsync("base_currency") ?? "";
        FullName = c.FullName;
        Phone = c.Phone;
        Address = c.Address;
        HasLocation = c is { Latitude: not null, Longitude: not null };
        _latitude = c.Latitude;
        _longitude = c.Longitude;
        HasDebt = c.DebtBalance > 0;
        DebtText = HasDebt ? BuildDebtText(c, currency) : Loc.Instance["no_debt"];
        LimitText = c.CreditLimit > 0 ? string.Format(Loc.Instance["limit_fmt"], c.CreditLimit, currency) : null;
        StaleText = string.Format(Loc.Instance["status_fmt"], await db.GetMetaAsync("last_sync") ?? "—");
    }

    private static string BuildDebtText(LocalCustomer c, string currency)
    {
        try
        {
            var balances = JsonSerializer.Deserialize<List<CurrencyAmountDto>>(c.DebtBalancesJson) ?? [];
            if (balances.Count > 1)
                return string.Join("\n", balances.Where(b => b.Amount != 0).Select(b => $"{b.Amount:N0} {b.Currency}"));
        }
        catch { }
        return $"{c.DebtBalance:N0} {currency}";
    }

    [ObservableProperty] private bool _hasLocation;
    private double? _latitude;
    private double? _longitude;

    [RelayCommand]
    private async Task OpenMapAsync()
    {
        if (_latitude is not { } lat || _longitude is not { } lng) return;
        await Launcher.OpenAsync(new Uri($"geo:0,0?q={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)},{lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}({Uri.EscapeDataString(FullName ?? "")})"));
    }

    [RelayCommand]
    private Task SaleAsync() =>
        Shell.Current.GoToAsync("sale", new Dictionary<string, object> { ["customerId"] = _customerId });

    [RelayCommand]
    private Task RepayAsync() =>
        Shell.Current.GoToAsync("repay", new Dictionary<string, object> { ["customerId"] = _customerId });

    [RelayCommand]
    private async Task CallAsync()
    {
        if (Phone is { Length: > 0 } phone && PhoneDialer.Default.IsSupported)
            PhoneDialer.Default.Open(phone);
        await Task.CompletedTask;
    }
}
