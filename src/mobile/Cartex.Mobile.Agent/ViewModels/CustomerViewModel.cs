using System.Text.Json;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Agent.Data;
using Cartex.Mobile.Agent.Services;
using Cartex.Shared.Models.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Agent.ViewModels;

public partial class CustomerViewModel(AgentDb db, ICustomersApi customersApi, SyncService sync) : ObservableObject, IQueryAttributable
{
    [ObservableProperty] private string _fullName = "";
    [ObservableProperty] private string _initials = "";
    [ObservableProperty] private string? _phone;
    [ObservableProperty] private string? _address;
    [ObservableProperty] private string _debtText = "";
    [ObservableProperty] private string _limitText = "—";
    [ObservableProperty] private string _ordersCount = "0";
    [ObservableProperty] private string _staleText = "";
    [ObservableProperty] private bool _hasDebt;
    [ObservableProperty] private bool _isBusy;

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
        Initials = string.Concat(c.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpper(w[0])));
        Phone = c.Phone;
        Address = c.Address;
        HasLocation = c is { Latitude: not null, Longitude: not null };
        _latitude = c.Latitude;
        _longitude = c.Longitude;
        HasDebt = c.DebtBalance > 0;
        DebtText = HasDebt ? BuildDebtText(c, currency) : Loc.Instance["no_debt"];
        LimitText = c.CreditLimit > 0 ? $"{c.CreditLimit:N0} {currency}" : "—";
        OrdersCount = (await db.GetOrdersAsync()).Count(o => o.CustomerId == _customerId).ToString();
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

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsBusy) return;
        var page = Shell.Current.CurrentPage;
        if (!await page.DisplayAlert(Loc.Instance["delete_customer"], Loc.Instance["delete_customer_confirm"],
                Loc.Instance["delete_customer"], Loc.Instance["cancel"]))
            return;
        IsBusy = true;
        try
        {
            await customersApi.DeleteAsync(_customerId);
            await sync.SyncAsync();
            Ui.Toast(Loc.Instance["customer_deleted"]);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Ui.Toast(ex is Refit.ApiException api ? SyncService.DescribeError(api) : Loc.Instance["err_no_connection"]);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
