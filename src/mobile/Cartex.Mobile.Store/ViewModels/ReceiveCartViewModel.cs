using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Suppliers;
using Cartex.Shared.Models.Supplies;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public partial class ReceiveCartViewModel : ObservableObject
{
    private readonly SupplyCartStore _cart;
    private readonly WarehouseContext _warehouse;
    private readonly ISuppliesApi _suppliesApi;
    private readonly ISuppliersApi _suppliersApi;
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<SupplyCartLine> Lines { get; } = [];
    public ObservableCollection<SupplierDto> Suppliers { get; } = [];

    [ObservableProperty] private bool _isEmpty = true;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasSupplier;
    [ObservableProperty] private string _supplierName = "";
    [ObservableProperty] private string _supplierSearch = "";
    [ObservableProperty] private bool _hasSuppliers;
    [ObservableProperty] private bool _isSupplierModalOpen;
    [ObservableProperty] private string _newSupplierName = "";
    [ObservableProperty] private string _newSupplierPhone = "";

    public ReceiveCartViewModel(SupplyCartStore cart, WarehouseContext warehouse, ISuppliesApi suppliesApi, ISuppliersApi suppliersApi)
    {
        _cart = cart;
        _warehouse = warehouse;
        _suppliesApi = suppliesApi;
        _suppliersApi = suppliersApi;
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
        if (e.PropertyName is not (nameof(SupplyCartLine.PurchasePrice) or nameof(SupplyCartLine.SellingPrice) or nameof(SupplyCartLine.Quantity))) return;
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
        SupplierName = _cart.SupplierName ?? "";
        HasSupplier = _cart.SupplierId is not null;
        OnPropertyChanged(nameof(TotalText));
    }

    partial void OnSupplierSearchChanged(string value) => _ = SearchSuppliersAsync(value);

    private async Task SearchSuppliersAsync(string term)
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        if (string.IsNullOrWhiteSpace(term))
        {
            Suppliers.Clear();
            HasSuppliers = false;
            return;
        }
        try
        {
            await Task.Delay(350, cts.Token);
            var rows = await _suppliersApi.GetAllAsync(term);
            if (cts.IsCancellationRequested) return;
            Suppliers.Clear();
            foreach (var supplier in rows.Take(10))
                Suppliers.Add(supplier);
            HasSuppliers = Suppliers.Count > 0;
        }
        catch (OperationCanceledException) { }
        catch
        {
            Suppliers.Clear();
            HasSuppliers = false;
        }
    }

    [RelayCommand]
    private void PickSupplier(SupplierDto supplier)
    {
        _cart.SetSupplier(supplier.Id, supplier.Name);
        SupplierSearch = "";
        Suppliers.Clear();
        HasSuppliers = false;
    }

    [RelayCommand]
    private void ClearSupplier() => _cart.SetSupplier(null, null);

    [RelayCommand]
    private void OpenSupplierModal()
    {
        NewSupplierName = SupplierSearch;
        NewSupplierPhone = "";
        IsSupplierModalOpen = true;
    }

    [RelayCommand]
    private void CloseSupplierModal() => IsSupplierModalOpen = false;

    [RelayCommand]
    private async Task SaveSupplierAsync()
    {
        if (string.IsNullOrWhiteSpace(NewSupplierName))
        {
            Ui.Toast(Loc.Instance["err_fill_all"]);
            return;
        }
        IsBusy = true;
        try
        {
            var id = await _suppliersApi.CreateAsync(new CreateSupplierRequest(NewSupplierName.Trim(),
                string.IsNullOrWhiteSpace(NewSupplierPhone) ? null : NewSupplierPhone.Trim()));
            _cart.SetSupplier(id, NewSupplierName.Trim());
            IsSupplierModalOpen = false;
            SupplierSearch = "";
            HasSuppliers = false;
        }
        catch (Refit.ApiException ex)
        {
            Ui.Toast(ApiErrors.Describe(ex));
        }
        catch { Ui.Toast(Loc.Instance["err_no_connection"]); }
        finally { IsBusy = false; }
    }

    /// Bir vaqtda bitta qator ochiq turadi: boshqasiga bosilganda avvalgisi yopiladi.
    [RelayCommand]
    private void ToggleExpand(SupplyCartLine line)
    {
        foreach (var other in Lines)
            if (!ReferenceEquals(other, line))
                other.IsExpanded = false;
        line.IsExpanded = !line.IsExpanded;
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
                _cart.SupplierId,
                _warehouse.WarehouseId!.Value,
                DateOnly.FromDateTime(DateTime.Today),
                _cart.Lines.Select(l => new CreateSupplyItemRequest(l.VariantId, l.Quantity, l.PurchasePrice, null,
                    SellingPrice: l.SellingPrice > 0 ? l.SellingPrice : null)).ToList()
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
