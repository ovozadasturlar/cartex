using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Paging;
using Cartex.ApiClient.Querying;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.Warehouses;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;
using Cartex.UI.Views;

namespace Cartex.UI.ViewModels;

public partial class ReturnsViewModel : ViewModelBase, ILoadable
{
    private readonly ICustomerReturnsApi _api;
    private readonly ISalesApi _salesApi;
    private readonly ICustomersApi _customersApi;
    private readonly IProductsApi _productsApi;
    private readonly BranchContextService _branch;
    private readonly IDialogService _dialog;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly AuthService _auth;
    private readonly PrintDispatchService _print;
    private readonly ISettingsApi _settingsApi;
    private readonly ReferenceCache _cache;
    private bool _allowReturnOnVoidedSale;
    private bool _allowFreeReturnLines = true;
    private bool _requireReturnReason;

    private long? _pendingSaleId;
    private string? _idempotencyKey;
    private CancellationTokenSource? _searchCts;

    public ReturnsViewModel(
        ICustomerReturnsApi api,
        ISalesApi salesApi,
        ICustomersApi customersApi,
        IProductsApi productsApi,
        BranchContextService branch,
        IDialogService dialog,
        IToastService toast,
        IBusyService busy,
        AuthService auth,
        PrintDispatchService print,
        ISettingsApi settingsApi,
        ReferenceCache cache)
    {
        _api = api;
        _salesApi = salesApi;
        _customersApi = customersApi;
        _productsApi = productsApi;
        _branch = branch;
        _dialog = dialog;
        _toast = toast;
        _busy = busy;
        _auth = auth;
        _print = print;
        _settingsApi = settingsApi;
        _cache = cache;
        Paging.Attach(LoadAsync);
        Paging.ConfigureSort([new(L["date"], "CreatedAt"), new(L["total"], "RefundAmount")], new(L["date"], "CreatedAt"));
        Paging.Descending = true;
        _auth.LoggedOut += ResetState;
    }

    public ObservableCollection<CustomerReturnListDto> Documents { get; } = [];
    public ObservableCollection<SelectableSaleRow> CustomerSales { get; } = [];
    public ObservableCollection<ReturnEditorLine> Lines { get; } = [];
    public ObservableCollection<WarehouseDto> Warehouses => _branch.Warehouses;
    public ObservableCollection<decimal> PriceOptions { get; } = [];
    public PaginationState Paging { get; } = new();

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddDays(-30);
    [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;
    [ObservableProperty] private bool _isEditorOpen;
    [ObservableProperty] private WarehouseDto? _selectedWarehouse;
    [ObservableProperty] private CustomerDto? _selectedCustomer;
    [ObservableProperty] private DateTimeOffset _businessDate = DateTimeOffset.Now;
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private bool _isSalesPanelOpen;
    [ObservableProperty] private ProductDto? _lineProduct;
    [ObservableProperty] private string _productSearch = "";
    [ObservableProperty] private decimal _lineQuantity = 1m;
    [ObservableProperty] private decimal _linePrice;
    [ObservableProperty] private bool _refundInCash;

    public bool IsEmpty => Documents.Count == 0;
    public bool CanView => _auth.HasPermission("returns.view");
    public bool CanCreate => _auth.HasPermission("returns.create");

    // QAYT-09: the permission says which staff may do it, the policy says whether the shop
    // does it at all. Both have to agree, and the server checks the same pair.
    public bool CanAddFreeLine => _auth.HasPermission("returns.freeLine") && _allowFreeReturnLines;
    public bool HasCustomer => SelectedCustomer is not null;
    public bool HasLines => Lines.Count > 0;
    public decimal TotalAmount => Lines.Sum(x => x.LineTotal);
    public bool NeedsManualSettlement => SelectedCustomer is null && Lines.Any(x => x.NeedsFreeLine);

    private void ResetState()
    {
        _searchCts?.Cancel();
        Documents.Clear();
        CloseEditor();
        SearchText = "";
        OnPropertyChanged(nameof(IsEmpty));
    }

    public async Task LoadAsync()
    {
        OnPropertyChanged(nameof(CanCreate));
        if (!CanView) return;
        await LoadPolicyAsync();
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var query = QueryRequest.Create()
                    .Page(Paging.Page, Paging.PageSize)
                    .Sort(Paging.SortBy, Paging.Descending)
                    .Search(string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim())
                    .With("fromDate", DateOnly.FromDateTime(DateFrom.Date))
                    .With("toDate", DateOnly.FromDateTime(DateTo.Date))
                    .Build();
                var paged = (await _api.QueryAsync(query)).ToPaged();
                Documents.Clear();
                foreach (var row in paged.Items) Documents.Add(row);
                Paging.Apply(paged.Meta);
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task LoadPolicyAsync()
    {
        try
        {
            var policy = await _cache.GetAsync(CacheKeys.SalesPolicy, _settingsApi.GetSalesPolicyAsync);
            _allowReturnOnVoidedSale = policy.AllowReturnOnVoidedSale;
            _allowFreeReturnLines = policy.AllowFreeReturnLines;
            _requireReturnReason = policy.RequireReturnReason;
            OnPropertyChanged(nameof(CanAddFreeLine));
        }
        catch { }
    }

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        _ = DebouncedAsync(cts.Token, () => { Paging.Page = 1; return LoadAsync(); });
    }

    partial void OnDateFromChanged(DateTimeOffset value) => _ = Refresh();
    partial void OnDateToChanged(DateTimeOffset value) => _ = Refresh();

    private static async Task DebouncedAsync(CancellationToken token, Func<Task> action)
    {
        try { await Task.Delay(300, token); } catch { return; }
        if (!token.IsCancellationRequested) await action();
    }

    [RelayCommand]
    private Task Refresh() { Paging.Page = 1; return LoadAsync(); }

    [RelayCommand]
    private async Task OpenDetailAsync(CustomerReturnListDto row)
    {
        if (row is null) return;
        try
        {
            CustomerReturnDocumentDto document;
            using (_busy.Begin(L["loading"])) document = await _api.GetByIdAsync(row.Id);
            await _dialog.ShowAsync<ReturnDetailDialog, ReturnDetailViewModel, bool>(
                new ReturnDetailViewModel(document, _print, _toast, _branch));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    // ==========================================
    // EDITOR
    // ==========================================

    [RelayCommand]
    private void NewReturn()
    {
        if (!CanCreate) return;
        _pendingSaleId = null;
        OpenEditor();
    }

    /// <summary>Opens the editor pre-loaded with a single sale, used from the sales history.</summary>
    public void StartForSale(long saleId)
    {
        if (!CanCreate) return;
        _pendingSaleId = saleId;
        OpenEditor();
    }

    private void OpenEditor()
    {
        _idempotencyKey = Guid.NewGuid().ToString("N");
        SelectedWarehouse = Warehouses.FirstOrDefault(x => x.Id == _branch.CurrentWarehouseId) ?? Warehouses.FirstOrDefault();
        SelectedCustomer = null;
        BusinessDate = DateTimeOffset.Now;
        Note = "";
        RefundInCash = false;
        IsSalesPanelOpen = false;
        CustomerSales.Clear();
        Lines.Clear();
        ClearLineDraft();
        IsEditorOpen = true;
        NotifyEditor();
        if (_pendingSaleId is { } saleId) _ = LoadPendingSaleAsync(saleId);
    }

    [RelayCommand]
    private void CloseEditor()
    {
        IsEditorOpen = false;
        _pendingSaleId = null;
        CustomerSales.Clear();
        Lines.Clear();
        NotifyEditor();
    }

    private async Task LoadPendingSaleAsync(long saleId)
    {
        try
        {
            SaleDetailDto detail;
            using (_busy.Begin(L["loading"])) detail = await _salesApi.GetByIdAsync(saleId);
            if (detail.CustomerId is { } customerId)
                SelectedCustomer = await _customersApi.GetByIdAsync(customerId);
            SelectedWarehouse = Warehouses.FirstOrDefault(x => x.Id == detail.WarehouseId) ?? SelectedWarehouse;
            AddSaleLines(detail);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        finally { _pendingSaleId = null; }
    }

    [RelayCommand]
    private async Task PickCustomerAsync()
    {
        var picker = ServiceLocator.Resolve<CustomerPickerViewModel>();
        var picked = await _dialog.ShowAsync<CustomerPickerDialog, CustomerPickerViewModel, CustomerDto>(picker);
        if (picked is null) return;
        SelectedCustomer = picked;
        await LoadCustomerSalesAsync();
    }

    [RelayCommand]
    private void ClearCustomer()
    {
        SelectedCustomer = null;
        CustomerSales.Clear();
        IsSalesPanelOpen = false;
        NotifyEditor();
    }

    private async Task LoadCustomerSalesAsync()
    {
        CustomerSales.Clear();
        if (SelectedCustomer is null) return;
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var query = QueryRequest.Create()
                    .Page(1, 50)
                    .Sort("CreatedAt", true)
                    .With("customerId", SelectedCustomer.Id)
                    .Build();
                var paged = (await _salesApi.QueryAsync(query)).ToPaged();
                // QAYT-08: a voided sale is only offered when the shop opened that door, so the
                // clerk never picks a sale that the save will then reject.
                foreach (var sale in paged.Items.Where(x => x.Status != "Returned"
                             && (x.Status != "Voided" || _allowReturnOnVoidedSale)))
                    CustomerSales.Add(new SelectableSaleRow(sale));
            }
            IsSalesPanelOpen = CustomerSales.Count > 0;
            if (!IsSalesPanelOpen) _toast.Info(L["ret_no_open_sales"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        NotifyEditor();
    }

    [RelayCommand]
    private void ToggleSalesPanel() => IsSalesPanelOpen = !IsSalesPanelOpen;

    [RelayCommand]
    private async Task LoadSelectedSalesAsync()
    {
        var picked = CustomerSales.Where(x => x.IsSelected).Select(x => x.Sale.Id).ToList();
        if (picked.Count == 0)
        {
            _toast.Error(L["ret_select_sales"]);
            return;
        }

        try
        {
            using (_busy.Begin(L["loading"]))
                foreach (var saleId in picked)
                    AddSaleLines(await _salesApi.GetByIdAsync(saleId));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        IsSalesPanelOpen = false;
        NotifyEditor();
    }

    private void AddSaleLines(SaleDetailDto detail)
    {
        foreach (var item in detail.Items)
        {
            var remaining = item.ReturnableQuantity > 0 ? item.ReturnableQuantity : item.Quantity - item.ReturnedQuantity;
            if (remaining <= 0) continue;

            var source = new ReturnSource(
                item.SaleItemId, detail.Id, NetUnitPrice(item), item.Quantity, remaining, detail.SaleDate);
            var existing = Lines.FirstOrDefault(x => x.VariantId == item.VariantId && x.IsFromSale);
            if (existing is not null)
            {
                existing.AddSource(source);
                continue;
            }
            Lines.Add(new ReturnEditorLine(item.VariantId, item.ProductName, item.UnitName, source, NotifyEditor));
        }
        NotifyEditor();
    }

    /// Qaytariladigan pul savdodagi chegirmadan keyingi qiymatdan hisoblanadi, katalog narxidan
    /// emas. Shuning uchun ekranda ham aynan o'sha narx ko'rsatiladi — aks holda kassir
    /// ko'rgan summa server qaytaradigan summadan farq qilardi.
    private static decimal NetUnitPrice(SaleDetailItemDto item) =>
        item.Quantity > 0 ? Math.Round(item.NetTotal / item.Quantity, 2) : item.UnitPrice;

    [RelayCommand]
    private void RemoveLine(ReturnEditorLine line)
    {
        Lines.Remove(line);
        NotifyEditor();
    }

    // ==========================================
    // FREE LINE
    // ==========================================

    // AutoCompleteBox only opens its drop-down for items it populated itself, so a
    // server-backed search has to go through AsyncPopulator rather than ItemsSource.
    public Func<string?, CancellationToken, Task<IEnumerable<object>>> ProductPopulator => SearchProductsAsync;

    private async Task<IEnumerable<object>> SearchProductsAsync(string? search, CancellationToken token)
    {
        if (!CanAddFreeLine || string.IsNullOrWhiteSpace(search)) return [];
        try { return (await _productsApi.GetAllAsync(search: search.Trim())).Take(20); }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); return []; }
    }

    partial void OnLineProductChanged(ProductDto? value) => _ = LoadPriceOptionsAsync(value);

    private async Task LoadPriceOptionsAsync(ProductDto? product)
    {
        PriceOptions.Clear();
        LinePrice = 0;
        if (product is null || product.DefaultVariantId <= 0) return;
        try
        {
            var prices = await _salesApi.GetVariantPricesAsync(product.DefaultVariantId, SelectedCustomer?.Id);
            foreach (var price in prices) PriceOptions.Add(price.UnitPrice);
            LinePrice = PriceOptions.FirstOrDefault();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void AddFreeLine()
    {
        if (!CanAddFreeLine) return;
        if (LineProduct is not { DefaultVariantId: > 0 } product)
        {
            _toast.Error(L["select_product"]);
            return;
        }
        if (LineQuantity <= 0 || LinePrice <= 0)
        {
            _toast.Error(L["ret_quantity_price_required"]);
            return;
        }

        Lines.Add(ReturnEditorLine.Free(
            product.DefaultVariantId, product.Name, product.UnitName, LineQuantity, LinePrice, NotifyEditor));
        ClearLineDraft();
        NotifyEditor();
    }

    /// Sahifadagi hamma narsani tozalaydi — kassir yarim to'ldirilgan qaytarishdan
    /// voz kechganda qolgan qatorlar keyingisiga qo'shilib ketmasligi uchun.
    [RelayCommand]
    private async Task ClearEditorAsync()
    {
        if (Lines.Count > 0 && !await _dialog.ConfirmAsync(L["clear_confirm"], L["clear"])) return;
        Lines.Clear();
        CustomerSales.Clear();
        IsSalesPanelOpen = false;
        SelectedCustomer = null;
        Note = "";
        RefundInCash = false;
        BusinessDate = DateTime.Today;
        _idempotencyKey = null;
        ClearLineDraft();
        NotifyEditor();
    }

    public bool CanClearEditor => Lines.Count > 0 || SelectedCustomer is not null || !string.IsNullOrWhiteSpace(Note);

    private void ClearLineDraft()
    {
        LineProduct = null;
        ProductSearch = "";
        LineQuantity = 1m;
        LinePrice = 0;
        PriceOptions.Clear();
    }

    private void NotifyEditor()
    {
        OnPropertyChanged(nameof(HasCustomer));
        OnPropertyChanged(nameof(HasLines));
        OnPropertyChanged(nameof(CanClearEditor));
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(NeedsManualSettlement));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanCreate || SelectedWarehouse is null) { _toast.Error(L["select_warehouse"]); return; }
        var rows = Lines.Where(x => x.Quantity > 0).ToList();
        if (rows.Count == 0) { _toast.Error(L["ret_select_qty"]); return; }

        // Savdodagidan ortiq miqdor yoki boshqa narx erkin qator bo'lib ketadi — serverda
        // qaytariladigan pulni faqat erkin qatorda kassir belgilay oladi (QAYT-04).
        if (rows.Any(x => x.NeedsFreeLine) && !CanAddFreeLine)
        {
            _toast.Error(L["ret_free_line_required"]);
            return;
        }

        if (_requireReturnReason && rows.Any(x => string.IsNullOrWhiteSpace(x.Reason)))
        {
            _toast.Error(L["ret_reason_required"]);
            return;
        }

        var requestLines = rows
            .SelectMany(row => row.Allocate().Select(part => new CustomerReturnLineRequest(
                row.VariantId, part.Quantity, part.SaleItemId,
                part.SaleItemId is null ? row.UnitPrice : null,
                row.Reason, row.Condition, row.Disposition)))
            .ToList();

        var settlements = NeedsManualSettlement
            ? new List<CustomerReturnSettlementRequest>
            {
                new(RefundInCash ? "Cash" : "NoCharge", "", rows.Sum(x => x.LineTotal))
            }
            : null;

        _idempotencyKey ??= Guid.NewGuid().ToString("N");
        try
        {
            CustomerReturnCreatedDto created;
            using (_busy.Begin(L["loading"]))
                created = await _api.CreateAsync(new CreateCustomerReturnRequest(
                    SelectedWarehouse.Id,
                    [.. requestLines],
                    SelectedCustomer?.Id,
                    settlements,
                    AutoSettle: settlements is null,
                    BusinessDate: DateOnly.FromDateTime(BusinessDate.Date),
                    Note: string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                    IdempotencyKey: _idempotencyKey));

            _toast.Success(string.Format(L["ret_created_fmt"], created.DocumentNumber));
            _idempotencyKey = null;
            CloseEditor();
            await Refresh();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}

public partial class SelectableSaleRow(SaleDto sale) : ObservableObject
{
    public SaleDto Sale { get; } = sale;
    [ObservableProperty] private bool _isSelected;
}

/// Bitta savdo qatori: qaysi savdodan, qanday narxda va nechtasi qaytarilishi mumkin.
public sealed record ReturnSource(
    long SaleItemId, long SaleId, decimal NetUnitPrice, decimal SoldQuantity, decimal Returnable, DateTime SoldAt);

/// Bir mahsulot bir necha savdoda sotilgan bo'lishi mumkin. Kassir uchun bu bitta qator —
/// "shu mahsulotdan 3 dona qaytdi" — qaysi savdoning qatoridan olinishini dastur hisoblaydi.
public partial class ReturnEditorLine : ObservableObject
{
    private readonly Action _onChanged;
    private readonly List<ReturnSource> _sources = [];

    private ReturnEditorLine(long variantId, string productName, string unitName, Action onChanged)
    {
        VariantId = variantId;
        ProductName = productName;
        UnitName = unitName;
        _onChanged = onChanged;
    }

    public ReturnEditorLine(long variantId, string productName, string unitName, ReturnSource source, Action onChanged)
        : this(variantId, productName, unitName, onChanged)
    {
        AddSource(source);
        _quantity = 0m;
    }

    public static ReturnEditorLine Free(
        long variantId, string productName, string unitName, decimal quantity, decimal unitPrice, Action onChanged) =>
        new(variantId, productName, unitName, onChanged) { Quantity = quantity, UnitPrice = unitPrice };

    public ObservableCollection<decimal> PriceOptions { get; } = [];

    public void AddSource(ReturnSource source)
    {
        _sources.Add(source);
        _sources.Sort((a, b) => b.SoldAt.CompareTo(a.SoldAt));

        PriceOptions.Clear();
        foreach (var price in _sources.Select(x => x.NetUnitPrice).Distinct())
            PriceOptions.Add(price);

        // Standart holatda oxirgi savdodagi narx turadi.
        UnitPrice = _sources[0].NetUnitPrice;
        OnPropertyChanged(nameof(TotalTaken));
        OnPropertyChanged(nameof(TakenLabel));
        OnPropertyChanged(nameof(Returnable));
        OnPropertyChanged(nameof(IsFromSale));
    }

    public long VariantId { get; }
    public string ProductName { get; }
    public string UnitName { get; }
    public bool IsFromSale => _sources.Count > 0;
    public bool IsFreeLine => _sources.Count == 0;

    /// Mijoz shu mahsulotdan jami nechta olib ketgani. Qabul qilayotgan odam aynan shu
    /// raqamga qarab miqdor kiritadi — savdo raqami unga kerak emas.
    public decimal TotalTaken => _sources.Sum(x => x.SoldQuantity);
    public string TakenLabel => IsFreeLine ? "—" : $"{TotalTaken:0.###}";

    /// Savdo qatorlaridan qaytarish mumkin bo'lgan qism. Undan ortig'i ham qabul qilinadi —
    /// ortiqchasi erkin qator bo'lib ketadi, ya'ni kassir 0 ga tushib qolgan maydonga emas,
    /// o'zi yozgan raqamga qaraydi.
    public decimal Returnable => _sources.Sum(x => x.Returnable);

    /// Savdodagi narxdan boshqasi yozilsa, server o'sha narxni faqat erkin qatorda hisobga
    /// oladi — shuning uchun bunday qator savdodan uzilib yuboriladi.
    public bool HasCustomPrice => IsFromSale && !_sources.Exists(x => x.NetUnitPrice == UnitPrice);

    /// Ortiqcha miqdor yoki boshqa narx — ikkalasi ham erkin qator sifatida ketadi.
    public bool NeedsFreeLine => IsFreeLine || HasCustomPrice || Quantity > Returnable;

    /// Kiritilgan miqdorni savdo qatorlariga bo'ladi: avval tanlangan narxdagilar, keyin
    /// eng yangisidan boshlab qolganlari. Sig'magani erkin qator bo'lib qo'shiladi.
    public IEnumerable<(long? SaleItemId, decimal Quantity)> Allocate()
    {
        if (IsFreeLine || HasCustomPrice)
        {
            yield return (null, Quantity);
            yield break;
        }

        var left = Quantity;
        foreach (var source in _sources.OrderBy(x => x.NetUnitPrice == UnitPrice ? 0 : 1).ThenByDescending(x => x.SoldAt))
        {
            if (left <= 0) yield break;
            var take = Math.Min(left, source.Returnable);
            if (take <= 0) continue;
            left -= take;
            yield return (source.SaleItemId, take);
        }
        if (left > 0) yield return (null, left);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal))]
    [NotifyPropertyChangedFor(nameof(NeedsFreeLine))]
    private decimal _quantity;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal))]
    [NotifyPropertyChangedFor(nameof(HasCustomPrice))]
    [NotifyPropertyChangedFor(nameof(NeedsFreeLine))]
    private decimal _unitPrice;

    [ObservableProperty] private string _condition = "Sellable";
    [ObservableProperty] private string _disposition = "SellableRestock";
    [ObservableProperty] private string? _reason;

    public decimal LineTotal => Quantity * UnitPrice;

    partial void OnQuantityChanged(decimal value) => _onChanged();
    partial void OnUnitPriceChanged(decimal value) => _onChanged();

    // Damaged goods should never silently go back onto the shelf.
    partial void OnConditionChanged(string value) => Disposition = value switch
    {
        "Sellable" => "SellableRestock",
        "Opened" => "Quarantine",
        "Damaged" => "Scrap",
        "Defective" => "SupplierClaim",
        _ => Disposition
    };
}
