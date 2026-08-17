using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Supplies;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public partial class ReceiveCartViewModel : ObservableObject
{
    private readonly SupplyCartStore _cart;
    private readonly WarehouseContext _warehouse;
    private readonly ISuppliesApi _suppliesApi;

    public ObservableCollection<SupplyCartLine> Lines { get; } = [];

    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private bool _isBusy;

    public ReceiveCartViewModel(SupplyCartStore cart, WarehouseContext warehouse, ISuppliesApi suppliesApi)
    {
        _cart = cart;
        _warehouse = warehouse;
        _suppliesApi = suppliesApi;
    }

    public void Appear()
    {
        _cart.Changed += Refresh;
        Refresh();
    }

    public void Disappear()
    {
        _cart.Changed -= Refresh;
        foreach (var line in Lines)
            line.PropertyChanged -= OnLineChanged;
    }

    private void OnLineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(SupplyCartLine.PurchasePrice) or nameof(SupplyCartLine.Quantity))) return;
        OnPropertyChanged(nameof(TotalText));
        _cart.PersistSoon();
    }

    private void Refresh()
    {
        if (!Lines.SequenceEqual(_cart.Lines))
        {
            foreach (var line in Lines)
                line.PropertyChanged -= OnLineChanged;
            Lines.Clear();
            foreach (var line in _cart.Lines)
            {
                line.PropertyChanged += OnLineChanged;
                Lines.Add(line);
            }
        }
        IsEmpty = Lines.Count == 0;
        OnPropertyChanged(nameof(TotalText));
    }

    [RelayCommand]
    private void Increment(SupplyCartLine line) => _cart.SetQuantity(line.VariantId, line.Quantity + 1m);

    [RelayCommand]
    private void Decrement(SupplyCartLine line)
    {
        _cart.SetQuantity(line.VariantId, Math.Max(1m, line.Quantity - 1m));
    }

    [RelayCommand]
    private void Remove(SupplyCartLine line) => _cart.Remove(line.VariantId);

    public string TotalText => Money.Text(Lines.Sum(l => l.LineTotal));

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy) return;
        if (_cart.Lines.Count == 0)
        {
            Ui.Toast(Loc.Instance["err_no_items"]);
            return;
        }
        // Tannarxsiz kirim foyda hisobotini va hamkor mukofotini jimgina buzadi, shuning
        // uchun narx majburiy: 0 qiymat ham ataylab kiritilgan bo'lishi kerak.
        if (_cart.Lines.FirstOrDefault(l => l.PurchasePrice <= 0) is { } priceless)
        {
            Ui.Toast(string.Format(Loc.Instance["err_purchase_price_required"], priceless.ProductName));
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
            var request = new CreateSupplyRequest(
                null,
                _warehouse.WarehouseId!.Value,
                DateOnly.FromDateTime(DateTime.Today),
                _cart.Lines.Select(l => new CreateSupplyItemRequest(l.VariantId, l.Quantity, l.PurchasePrice, null)).ToList()
            );
            await _suppliesApi.CreateAsync(request);
            _cart.Clear();
            Ui.Toast(Loc.Instance["saved_successfully"]);
            await Shell.Current.GoToAsync("..");
        }
        catch (Refit.ApiException ex)
        {
            await Shell.Current.CurrentPage.DisplayAlertAsync(Loc.Instance["error"], ApiErrors.Describe(ex), Loc.Instance["ok"]);
        }
        catch
        {
            Ui.Toast(Loc.Instance["err_no_connection"]);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
