using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Shared.Models.Suppliers;
using Cartex.Shared.Models.Supplies;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.Mobile.Store.ViewModels;

public partial class ReceiveCartViewModel : AccessAwareViewModel
{
    private readonly SupplyCartStore _cart;
    private readonly WarehouseContext _warehouse;
    private readonly ISuppliesApi _suppliesApi;
    private readonly ISuppliersApi _suppliersApi;
    private readonly MobileOfflineService _offline;
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
    [ObservableProperty] private bool _isLineModalOpen;
    [ObservableProperty] private SupplyCartLine? _selectedLine;
    [ObservableProperty] private string _selectedQtyText = "";
    public bool CanReceiveStock => Access.CanReceiveStock;

    // Ochiq swipe'ni yopish uchun qator bosilganda chaqiriladi; sahifa biror drawer
    // yopilganini qaytaradi — u holda bosish faqat yopish deb qabul qilinadi.
    public Func<bool>? RowInteracted { get; set; }

    public ReceiveCartViewModel(SupplyCartStore cart, WarehouseContext warehouse, ISuppliesApi suppliesApi, ISuppliersApi suppliersApi, MobileOfflineService offline, AccessState access)
        : base(access)
    {
        _cart = cart;
        _warehouse = warehouse;
        _suppliesApi = suppliesApi;
        _suppliersApi = suppliersApi;
        _offline = offline;
        ObserveAccess(nameof(CanReceiveStock));
    }

    public void Appear()
    {
        if (!CanReceiveStock) return;
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
        var cts = Debounce.Restart(ref _searchCts);
        if (string.IsNullOrWhiteSpace(term))
        {
            Suppliers.Clear();
            HasSuppliers = false;
            return;
        }
        try
        {
            await Task.Delay(350, cts.Token);
            var rows = _offline.ShouldUseOffline
                ? await _offline.SearchSuppliersAsync(term, 10)
                : await _suppliersApi.GetAllAsync(term);
            if (cts.IsCancellationRequested) return;
            ShowSuppliers(rows);
        }
        catch (OperationCanceledException) { }
        catch
        {
            _offline.MarkServerUnavailable();
            if (_offline.IsEnabled && !cts.IsCancellationRequested)
                ShowSuppliers(await _offline.SearchSuppliersAsync(term, 10));
            else
                ShowSuppliers([]);
        }
    }

    private void ShowSuppliers(IReadOnlyList<SupplierDto> rows)
    {
        Suppliers.Clear();
        foreach (var supplier in rows.Take(10))
            Suppliers.Add(supplier);
        HasSuppliers = Suppliers.Count > 0;
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
        if (_offline.ShouldUseOffline)
        {
            Ui.Toast(Loc.Instance["offline_mutation_blocked"]);
            return;
        }
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

    // Bir vaqtda bitta qator ochiq turadi: boshqasiga bosilganda avvalgisi yopiladi.
    [RelayCommand]
    private void ToggleExpand(SupplyCartLine line)
    {
        if (RowInteracted?.Invoke() == true) return;
        foreach (var other in Lines)
            if (!ReferenceEquals(other, line))
                other.IsExpanded = false;
        if (!line.IsExpanded)
            line.RefreshPriceTexts();
        line.IsExpanded = !line.IsExpanded;
    }

    [RelayCommand]
    private void OpenLineModal(SupplyCartLine line)
    {
        if (RowInteracted?.Invoke() == true) return;
        foreach (var other in Lines)
            other.IsExpanded = false;
        SelectedLine = line;
        SelectedQtyText = QuantityInput.Format(line.Quantity);
        line.RefreshPriceTexts();
        IsLineModalOpen = true;
    }

    [RelayCommand]
    private void CloseLineModal() => IsLineModalOpen = false;

    [RelayCommand]
    private void IncrementSelected()
    {
        if (SelectedLine is not { } line) return;
        _cart.SetQuantity(line.VariantId, line.Quantity + 1m);
        SelectedQtyText = QuantityInput.Format(line.Quantity);
    }

    [RelayCommand]
    private void DecrementSelected()
    {
        if (SelectedLine is not { } line) return;
        var next = Math.Max(1m, line.Quantity - 1m);
        if (next < line.Quantity)
            _cart.SetQuantity(line.VariantId, next);
        SelectedQtyText = QuantityInput.Format(line.Quantity);
    }

    [RelayCommand]
    private void SetSelectedQuantityFromText()
    {
        if (SelectedLine is not { } line) return;
        if (QuantityInput.TryParse(SelectedQtyText, allowsFractional: true, out var quantity, out var error))
        {
            _cart.SetQuantity(line.VariantId, quantity);
            SelectedQtyText = QuantityInput.Format(quantity);
        }
        else
        {
            SelectedQtyText = QuantityInput.Format(line.Quantity);
            Ui.Toast(Loc.Instance[error switch
            {
                QuantityInputError.MustBePositive => "quantity_positive_required",
                QuantityInputError.FractionNotAllowed => "quantity_integer_required",
                _ => "quantity_invalid"
            }]);
        }
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedLine is not { } line) return;
        // Avval tanlov bo'shatiladi: modal yopilishida fokusdan chiqqan miqdor maydoni
        // o'chirilayotgan qatorga qarshi validatsiya toast'ini otmasin.
        SelectedLine = null;
        IsLineModalOpen = false;
        _cart.Remove(line.VariantId);
    }

    [RelayCommand]
    private void Increment(SupplyCartLine line) => _cart.SetQuantity(line.VariantId, line.Quantity + 1m);

    [RelayCommand]
    private void Decrement(SupplyCartLine line)
    {
        var next = Math.Max(1m, line.Quantity - 1m);
        if (next < line.Quantity)
            _cart.SetQuantity(line.VariantId, next);
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
        if (_offline.ShouldUseOffline)
        {
            await SubmitOfflineAsync();
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

    private async Task SubmitOfflineAsync()
    {
        if (!_offline.SuppliesCapability)
        {
            Ui.Toast(Loc.Instance["offline_capability_off"]);
            return;
        }
        if (Access.SalesPolicy.RequireSupplier && _cart.SupplierId is null)
        {
            Ui.Toast(Loc.Instance["err_select_supplier"]);
            return;
        }
        if (_warehouse.WarehouseId is not { } warehouseId)
        {
            Ui.Toast(Loc.Instance["warehouse_none"]);
            return;
        }
        IsBusy = true;
        try
        {
            await _offline.EnqueueSupplyAsync(new MobileOfflineSupplyDraft(
                _cart.SupplierId, warehouseId,
                _cart.Lines.Select(l => new MobileOfflineSupplyLineDraft(l.VariantId, l.Quantity, l.PurchasePrice,
                    l.SellingPrice > 0 ? l.SellingPrice : null)).ToList()));
            _cart.Clear();
            Ui.Toast(Loc.Instance["offline_supply_queued"]);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Ui.Toast(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
