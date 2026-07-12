using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Ordering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CheckoutViewModel(IOrderingApi orderingApi) : ObservableObject, IQueryAttributable
{
    public ObservableCollection<CheckoutLine> Items { get; } = [];

    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _customerName = "";
    [ObservableProperty] private bool _hasCustomer;
    [ObservableProperty] private string _noteText = "";
    [ObservableProperty] private bool _hasNote;
    [ObservableProperty] private string _totalText = "";
    [ObservableProperty] private string _cashText = "";
    [ObservableProperty] private string _cardText = "";
    [ObservableProperty] private string _bonusText = "";
    [ObservableProperty] private string _paidText = "";
    [ObservableProperty] private string _changeText = "";
    [ObservableProperty] private string _debtText = "";

    private string _code = "";
    private CartDto? _cart;
    private readonly string _idempotencyKey = Guid.NewGuid().ToString();

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("code", out var code))
            _code = code.ToString() ?? "";
    }

    public async Task AppearAsync()
    {
        if (_cart is not null) return;
        try
        {
            _cart = await orderingApi.GetByCodeAsync(_code);
        }
        catch { }
        if (_cart is null)
        {
            await Shell.Current.CurrentPage.DisplayAlert(Loc.Instance["checkout"], Loc.Instance["cart_not_found"], Loc.Instance["ok"]);
            await Shell.Current.GoToAsync("..");
            return;
        }
        Items.Clear();
        foreach (var i in _cart.Items)
            Items.Add(new CheckoutLine(i));
        CustomerName = _cart.CustomerName ?? "";
        HasCustomer = _cart.CustomerId is not null;
        NoteText = _cart.Note ?? "";
        HasNote = !string.IsNullOrEmpty(_cart.Note);
        TotalText = $"{_cart.Total:N0} UZS";
        IsLoaded = true;
        Recalc();
    }

    partial void OnCashTextChanged(string value) => Recalc();
    partial void OnCardTextChanged(string value) => Recalc();
    partial void OnBonusTextChanged(string value) => Recalc();

    [RelayCommand]
    private void Exact()
    {
        if (_cart is null) return;
        CashText = _cart.Total.ToString("0");
    }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        if (_cart is null || IsBusy) return;
        var page = Shell.Current.CurrentPage;
        if (_cart.Total - Paid > 0 && _cart.CustomerId is null)
        {
            await page.DisplayAlert(Loc.Instance["checkout"], Loc.Instance["err_debt_needs_customer"], Loc.Instance["ok"]);
            return;
        }
        IsBusy = true;
        try
        {
            await orderingApi.CheckoutAsync(_code, new CheckoutCartRequest(Parse(CashText), Parse(CardText), Parse(BonusText), _idempotencyKey));
            Ui.Toast(Loc.Instance["sale_done"]);
            await Shell.Current.GoToAsync("..");
        }
        catch (ApiException ex)
        {
            await page.DisplayAlert(Loc.Instance["checkout"], ApiErrors.Describe(ex), Loc.Instance["ok"]);
        }
        catch
        {
            await page.DisplayAlert(Loc.Instance["checkout"], Loc.Instance["err_no_connection"], Loc.Instance["ok"]);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private decimal Paid => Parse(CashText) + Parse(CardText) + Parse(BonusText);

    private static decimal Parse(string? text) =>
        decimal.TryParse(text?.Replace(" ", ""), out var value) && value > 0 ? value : 0;

    private void Recalc()
    {
        if (_cart is null) return;
        var paid = Paid;
        PaidText = $"{paid:N0} UZS";
        var change = paid - _cart.Total;
        ChangeText = change > 0 && Parse(CashText) > 0 ? $"{change:N0} UZS" : "";
        var debt = _cart.Total - paid;
        DebtText = debt > 0 ? $"{debt:N0} UZS" : "";
    }
}

public sealed record CheckoutLine(CartItemDto Item)
{
    public string Name => Item.ProductName;
    public string QtyPriceText => $"{Item.Quantity:0.###} x {Item.UnitPrice:N0}";
    public string LineTotalText => $"{Item.LineTotal:N0}";
}
