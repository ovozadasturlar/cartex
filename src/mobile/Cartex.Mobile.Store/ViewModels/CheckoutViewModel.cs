using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Ordering;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Refit;

namespace Cartex.Mobile.Store.ViewModels;

public partial class CheckoutViewModel : ObservableObject, IQueryAttributable
{
    private readonly IOrderingApi _orderingApi;
    private readonly CartStore _localCart;
    private readonly WarehouseContext _warehouse;
    private readonly MobilePermissions _permissions;

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
    [ObservableProperty] private bool _canSelfSell;
    
    private string _code = "";
    private CartDto? _serverCart;
    private decimal _totalAmount;
    private long? _customerId;
    private readonly string _idempotencyKey = Guid.NewGuid().ToString();

    public CheckoutViewModel(IOrderingApi orderingApi, CartStore localCart, WarehouseContext warehouse, MobilePermissions permissions)
    {
        _orderingApi = orderingApi;
        _localCart = localCart;
        _warehouse = warehouse;
        _permissions = permissions;
        CanSelfSell = _permissions.Has("sales.create");
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("code", out var code))
            _code = code.ToString() ?? "";
    }

    public async Task AppearAsync()
    {
        if (IsLoaded) return;
        
        if (!string.IsNullOrEmpty(_code))
        {
            try { _serverCart = await _orderingApi.GetByCodeAsync(_code); }
            catch { }

            if (_serverCart is null)
            {
                await Shell.Current.CurrentPage.DisplayAlert(Loc.Instance["checkout"], Loc.Instance["cart_not_found"], Loc.Instance["ok"]);
                await Shell.Current.GoToAsync("..");
                return;
            }

            Items.Clear();
            foreach (var i in _serverCart.Items)
                Items.Add(new CheckoutLine(i.ProductName, i.Quantity, i.UnitPrice, i.LineTotal));
            
            _customerId = _serverCart.CustomerId;
            CustomerName = _serverCart.CustomerName ?? "";
            NoteText = _serverCart.Note ?? "";
            _totalAmount = _serverCart.Total;
        }
        else
        {
            Items.Clear();
            foreach (var l in _localCart.Lines)
                Items.Add(new CheckoutLine(l.ProductName, l.Quantity, l.UnitPrice, l.LineTotal));
            
            _customerId = _localCart.CustomerId;
            CustomerName = _localCart.CustomerName ?? "";
            NoteText = _localCart.Note ?? "";
            _totalAmount = _localCart.Total;
        }

        HasCustomer = _customerId is not null;
        HasNote = !string.IsNullOrEmpty(NoteText);
        TotalText = $"{_totalAmount:N0} UZS";
        IsLoaded = true;
        Recalc();
    }

    partial void OnCashTextChanged(string value) => Recalc();
    partial void OnCardTextChanged(string value) => Recalc();
    partial void OnBonusTextChanged(string value) => Recalc();

    [RelayCommand]
    private void Exact() => CashText = _totalAmount.ToString("0");

    [RelayCommand]
    private async Task QueueAsync()
    {
        if (IsBusy) return;
        var page = Shell.Current.CurrentPage;
        
        if (!string.IsNullOrEmpty(_code))
        {
            // Already queued
            await Shell.Current.GoToAsync("..");
            return;
        }
        
        if (!await _warehouse.EnsureSelectedAsync())
        {
            Ui.Toast(Loc.Instance["warehouse_none"]);
            return;
        }
        
        IsBusy = true;
        try
        {
            var request = new SubmitCartRequest(
                _warehouse.WarehouseId!.Value,
                _customerId,
                _localCart.Lines.Select(l => new SubmitCartItemRequest(l.VariantId, l.Quantity)).ToList(),
                Guid.NewGuid().ToString("N"),
                string.IsNullOrWhiteSpace(NoteText) ? null : NoteText);
            
            await _orderingApi.SubmitAsync(request);
            _localCart.Clear();
            Ui.Toast(Loc.Instance["send_to_queue"]);
            await Shell.Current.GoToAsync("..");
        }
        catch (ApiException ex) { await page.DisplayAlert(Loc.Instance["error"], ApiErrors.Describe(ex), Loc.Instance["ok"]); }
        catch { Ui.Toast(Loc.Instance["err_no_connection"]); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        if (IsBusy) return;
        var page = Shell.Current.CurrentPage;
        
        if (_totalAmount - Paid > 0 && _customerId is null)
        {
            await page.DisplayAlert(Loc.Instance["checkout"], Loc.Instance["err_debt_needs_customer"], Loc.Instance["ok"]);
            return;
        }
        
        IsBusy = true;
        try
        {
            string codeToCheckout = _code;
            
            // If local cart, we must submit to server first to get a code
            if (string.IsNullOrEmpty(codeToCheckout))
            {
                if (!await _warehouse.EnsureSelectedAsync())
                {
                    Ui.Toast(Loc.Instance["warehouse_none"]);
                    return;
                }
                
                var req = new SubmitCartRequest(
                    _warehouse.WarehouseId!.Value,
                    _customerId,
                    _localCart.Lines.Select(l => new SubmitCartItemRequest(l.VariantId, l.Quantity)).ToList(),
                    Guid.NewGuid().ToString("N"),
                    string.IsNullOrWhiteSpace(NoteText) ? null : NoteText);
                
                codeToCheckout = await _orderingApi.SubmitAsync(req);
                _localCart.Clear(); // successfully synced to server
            }

            await _orderingApi.CheckoutAsync(codeToCheckout, new CheckoutCartRequest(Parse(CashText), Parse(CardText), Parse(BonusText), _idempotencyKey));
            Ui.Toast(Loc.Instance["sale_done"]);
            await Shell.Current.GoToAsync("..");
        }
        catch (ApiException ex) { await page.DisplayAlert(Loc.Instance["checkout"], ApiErrors.Describe(ex), Loc.Instance["ok"]); }
        catch { await page.DisplayAlert(Loc.Instance["checkout"], Loc.Instance["err_no_connection"], Loc.Instance["ok"]); }
        finally { IsBusy = false; }
    }

    private decimal Paid => Parse(CashText) + Parse(CardText) + Parse(BonusText);

    private static decimal Parse(string? text) =>
        decimal.TryParse(text?.Replace(" ", ""), out var value) && value > 0 ? value : 0;

    private void Recalc()
    {
        var paid = Paid;
        PaidText = $"{paid:N0} UZS";
        var change = paid - _totalAmount;
        ChangeText = change > 0 && Parse(CashText) > 0 ? $"{change:N0} UZS" : "";
        var debt = _totalAmount - paid;
        DebtText = debt > 0 ? $"{debt:N0} UZS" : "";
    }
}

public sealed record CheckoutLine(string Name, decimal Quantity, decimal UnitPrice, decimal LineTotal)
{
    public string QtyPriceText => $"{Quantity:0.###} x {UnitPrice:N0}";
    public string LineTotalText => $"{LineTotal:N0}";
}
