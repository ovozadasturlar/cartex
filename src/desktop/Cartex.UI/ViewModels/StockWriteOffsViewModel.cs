using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.Products;
using Cartex.Shared.Models.Stocks;
using Cartex.Shared.Models.Warehouses;
using Cartex.UI.Models;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.UI.ViewModels;

public sealed record ReasonOption(string? Code, string Name)
{
    public override string ToString() => Name;
}

public sealed record WriteOffBatchOption(WriteOffBatchDto Source, string Label)
{
    public override string ToString() => Label;
}

public partial class StockWriteOffsViewModel : ViewModelBase, ILoadable
{
    /// BRAK-01: sabab ro'yxatdan tanlanadi, erkin matn emas — aks holda hisobot chiqmaydi.
    public static readonly string[] Reasons = ["Broken", "Expired", "Lost", "Stolen"];

    private const int ExportPageSize = 200;

    private readonly IStockWriteOffsApi _api;
    private readonly IProductsApi _productsApi;
    private readonly BranchContextService _branch;
    private readonly IDialogService _dialog;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly AuthService _auth;

    private string? _idempotencyKey;
    private bool _filtersReady;

    public StockWriteOffsViewModel(
        IStockWriteOffsApi api,
        IProductsApi productsApi,
        BranchContextService branch,
        IDialogService dialog,
        IToastService toast,
        IBusyService busy,
        IExportService export,
        AuthService auth)
    {
        _api = api;
        _productsApi = productsApi;
        _branch = branch;
        _dialog = dialog;
        _toast = toast;
        _busy = busy;
        _export = export;
        _auth = auth;
        Paging.Attach(LoadDocumentsAsync);
        _auth.LoggedOut += ResetState;
    }

    public ObservableCollection<WriteOffRow> Documents { get; } = [];
    public ObservableCollection<WriteOffBalanceDto> Balances { get; } = [];
    public ObservableCollection<WriteOffEditorLine> Lines { get; } = [];
    public ObservableCollection<WarehouseDto> Warehouses => _branch.Warehouses;
    public ObservableCollection<IdOption> FilterWarehouses { get; } = [];
    public ObservableCollection<ReasonOption> FilterReasons { get; } = [];
    public PaginationState Paging { get; } = new();

    [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddDays(-30);
    [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;
    [ObservableProperty] private IdOption? _filterWarehouse;
    [ObservableProperty] private ReasonOption? _filterReason;

    [ObservableProperty] private bool _isEditorOpen;
    [ObservableProperty] private WarehouseDto? _editorWarehouse;
    [ObservableProperty] private DateTimeOffset _businessDate = DateTimeOffset.Now;
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private ProductDto? _lineProduct;
    [ObservableProperty] private string _productSearch = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedDocument))]
    [NotifyPropertyChangedFor(nameof(CanReverseSelected))]
    private WriteOffRow? _selectedRow;

    [ObservableProperty] private bool _isDetailOpen;

    private static readonly StockWriteOffDto EmptyDocument =
        new(0, "", default, default, 0, "", null, 0, 0, null, null, []);

    public StockWriteOffDto SelectedDocument => SelectedRow?.Document ?? EmptyDocument;
    public bool CanReverseSelected => SelectedRow?.CanReverse == true;

    public bool CanWriteOff => _auth.HasPermission("stocks.writeOff");
    // Hisobot va qoldiqlar `stocks.view` bilan ochiladi; faqat chiqim ruxsati bor xodim
    // hujjat yozadi, lekin jurnalni ko'rmaydi — u holda so'rov ham yuborilmaydi.
    public bool CanViewReport => _auth.HasPermission("stocks.view");
    public bool CanExport => _auth.HasPermission("reports.export") && CanViewReport;
    public bool IsEmpty => Documents.Count == 0;
    public bool NoBalances => Balances.Count == 0;
    public bool HasLines => Lines.Count > 0;
    public decimal TotalCost => Lines.Sum(x => x.LineCost);
    public decimal ClaimTotal => Lines.Where(x => x.IsClaim).Sum(x => x.LineCost);

    /// BRAK-03: bitta mahsulotning partiyalari turli kirimdan kelgan bo'lishi mumkin, ya'ni
    /// bittasini ta'minotchiga qaytarish mumkin, boshqasini yo'q. Buni yashirmaslik kerak —
    /// omborchi qaysi jismoniy tovar chiqayotganini tanlayapti.
    public bool HasMixedClaimBatches => Lines.Any(x => x.HasMixedBatches);

    private IReadOnlyList<PageShortcut>? _shortcuts;
    public IReadOnlyList<PageShortcut> Shortcuts =>
        _shortcuts ??= CrudShortcuts(OpenEditorCommand, SaveCommand, CloseEditor, () => IsEditorOpen);

    private void ResetState()
    {
        _filtersReady = false;
        Documents.Clear();
        Balances.Clear();
        Lines.Clear();
        SelectedRow = null;
        IsDetailOpen = false;
        IsEditorOpen = false;
        Paging.Page = 1;
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(NoBalances));
        NotifyEditor();
    }

    public override void OnNavigatedFrom() => IsDetailOpen = false;

    public async Task LoadAsync()
    {
        OnPropertyChanged(nameof(CanWriteOff));
        OnPropertyChanged(nameof(CanViewReport));
        OnPropertyChanged(nameof(CanExport));

        _filtersReady = false;
        FilterWarehouses.Clear();
        FilterWarehouses.Add(new IdOption(null, L["all"]));
        foreach (var warehouse in Warehouses)
            FilterWarehouses.Add(new IdOption(warehouse.Id, warehouse.Name));
        FilterWarehouse = FilterWarehouses[0];

        FilterReasons.Clear();
        FilterReasons.Add(new ReasonOption(null, L["all"]));
        foreach (var reason in Reasons)
            FilterReasons.Add(new ReasonOption(reason, L[$"write_off_reason_{reason.ToLowerInvariant()}"]));
        FilterReason = FilterReasons[0];

        EditorWarehouse ??= Warehouses.FirstOrDefault(x => x.Id == _branch.CurrentWarehouseId)
            ?? Warehouses.FirstOrDefault();

        using (_busy.Begin(L["loading"]))
        {
            await LoadDocumentsAsync();
            await LoadBalancesAsync();
        }
        _filtersReady = true;
    }

    private async Task LoadDocumentsAsync()
    {
        if (!CanViewReport) return;
        try
        {
            var paged = (await _api.QueryAsync(
                DateOnly.FromDateTime(DateFrom.Date),
                DateOnly.FromDateTime(DateTo.Date),
                FilterWarehouse?.Id,
                FilterReason?.Code,
                Paging.Page,
                Paging.PageSize)).ToPaged();

            var reversed = paged.Items.Select(x => x.ReversesDocumentId).OfType<long>().ToHashSet();
            Documents.Clear();
            foreach (var row in paged.Items)
                Documents.Add(new WriteOffRow(row, reversed.Contains(row.Id), CanWriteOff));
            Paging.Apply(paged.Meta);
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    /// OMBOR-04: brak va ta'minotchiga da'vo qoldiqlari alohida saqlanmaydi — ular serverda
    /// harakatlardan hisoblanadi. Ekran ularni faqat ko'rsatadi, o'zi hisoblamaydi.
    private async Task LoadBalancesAsync()
    {
        if (!CanViewReport) return;
        try
        {
            var rows = await _api.GetBalancesAsync(FilterWarehouse?.Id);
            Balances.Clear();
            foreach (var row in rows) Balances.Add(row);
            OnPropertyChanged(nameof(NoBalances));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    partial void OnDateFromChanged(DateTimeOffset value) => ReloadOnFilterChange();
    partial void OnDateToChanged(DateTimeOffset value) => ReloadOnFilterChange();
    partial void OnFilterWarehouseChanged(IdOption? value) => ReloadOnFilterChange();
    partial void OnFilterReasonChanged(ReasonOption? value) => ReloadOnFilterChange();

    private void ReloadOnFilterChange()
    {
        if (_filtersReady) _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Paging.Page = 1;
        await LoadDocumentsAsync();
        await LoadBalancesAsync();
    }

    [RelayCommand]
    private async Task ExportAsync(string format)
    {
        if (!CanExport) return;
        try
        {
            // Ko'rinib turgan sahifa emas, tanlangan davr chiqariladi.
            var rows = (await _api.QueryAsync(
                DateOnly.FromDateTime(DateFrom.Date),
                DateOnly.FromDateTime(DateTo.Date),
                FilterWarehouse?.Id,
                FilterReason?.Code,
                1,
                ExportPageSize)).Content ?? [];

            await _export.ExportAsync(L["write_offs"], rows,
            [
                new(L["write_off_document"], x => x.DocumentNumber),
                new(L["date"], x => x.BusinessDate.ToDateTime(TimeOnly.MinValue)),
                new(L["warehouse"], x => x.WarehouseName),
                new(L["user"], x => x.UserName),
                new(L["total"], x => x.TotalCost),
                new(L["write_off_claim_total"], x => x.SupplierClaimAmount),
                new(L["note"], x => x.Note),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void OpenDetail(WriteOffRow row)
    {
        if (row is null) return;
        SelectedRow = row;
        IsDetailOpen = true;
    }

    [RelayCommand]
    private void CloseDetail() => IsDetailOpen = false;

    /// BRAK-07: chiqim tahrirlanmaydi va o'chirilmaydi. Yagona tuzatish yo'li — teskari amal,
    /// va ikkala hujjat ham jurnalda qoladi.
    [RelayCommand]
    private async Task ReverseAsync(WriteOffRow row)
    {
        if (row is null || !row.CanReverse) return;
        var message = string.Format(L["write_off_reverse_confirm"], row.Document.DocumentNumber);
        if (!await _dialog.ConfirmDangerAsync(message, L["write_off_reverse"])) return;

        try
        {
            StockWriteOffCreatedDto created;
            using (_busy.Begin(L["loading"]))
                created = await _api.ReverseAsync(row.Document.Id,
                    new ReverseStockWriteOffRequest(IdempotencyKey: Guid.NewGuid().ToString("N")));
            _toast.Success(string.Format(L["write_off_created_fmt"], created.DocumentNumber));
            IsDetailOpen = false;
            await RefreshAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void OpenEditor()
    {
        if (!CanWriteOff) return;
        _idempotencyKey = Guid.NewGuid().ToString("N");
        EditorWarehouse = Warehouses.FirstOrDefault(x => x.Id == _branch.CurrentWarehouseId)
            ?? Warehouses.FirstOrDefault();
        BusinessDate = DateTimeOffset.Now;
        Note = "";
        Lines.Clear();
        LineProduct = null;
        ProductSearch = "";
        IsEditorOpen = true;
        NotifyEditor();
    }

    [RelayCommand]
    private void CloseEditor()
    {
        IsEditorOpen = false;
        Lines.Clear();
        NotifyEditor();
    }

    // AutoCompleteBox faqat o'zi to'ldirgan ro'yxatni ochadi, shuning uchun serverdagi qidiruv
    // ItemsSource orqali emas, AsyncPopulator orqali beriladi.
    public Func<string?, CancellationToken, Task<IEnumerable<object>>> ProductPopulator => SearchProductsAsync;

    private async Task<IEnumerable<object>> SearchProductsAsync(string? search, CancellationToken token)
    {
        if (!CanWriteOff || string.IsNullOrWhiteSpace(search)) return [];
        try { return (await _productsApi.GetAllAsync(search: search.Trim())).Take(20); }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); return []; }
    }

    partial void OnLineProductChanged(ProductDto? value) => _ = AddLineAsync(value);

    /// BRAK-03/BRAK-04: partiyalar va ularning qaytarish imkoni serverdan olinadi. Klient
    /// taxmin qilmaydi — «Ta'minotchiga da'vo» varianti faqat server ruxsat bergan partiyada
    /// chiqadi, aks holda saqlash paytida rad etilardi.
    private async Task AddLineAsync(ProductDto? product)
    {
        if (product is not { DefaultVariantId: > 0 } || EditorWarehouse is null) return;
        try
        {
            List<WriteOffBatchDto> batches;
            using (_busy.Begin(L["loading"]))
                batches = await _api.GetBatchesAsync(EditorWarehouse.Id, product.DefaultVariantId);
            if (batches.Count == 0)
            {
                _toast.Error(L["write_off_no_batches"]);
                return;
            }
            Lines.Add(new WriteOffEditorLine(product.DefaultVariantId, product.Name, product.UnitName, batches, NotifyEditor));
            NotifyEditor();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        finally
        {
            LineProduct = null;
            ProductSearch = "";
        }
    }

    [RelayCommand]
    private void RemoveLine(WriteOffEditorLine line)
    {
        Lines.Remove(line);
        NotifyEditor();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanWriteOff) return;
        if (EditorWarehouse is null) { _toast.Error(L["select_warehouse"]); return; }

        var rows = Lines.Where(x => x.Quantity > 0).ToList();
        if (rows.Count == 0) { _toast.Error(L["write_off_no_quantity"]); return; }
        if (rows.Exists(x => x.Batch is null)) { _toast.Error(L["write_off_select_batch"]); return; }
        if (rows.Exists(x => x.Quantity > x.Batch!.Source.Quantity)) { _toast.Error(L["write_off_quantity_exceeds"]); return; }

        _idempotencyKey ??= Guid.NewGuid().ToString("N");
        try
        {
            StockWriteOffCreatedDto created;
            using (_busy.Begin(L["loading"]))
                created = await _api.CreateAsync(new CreateStockWriteOffRequest(
                    EditorWarehouse.Id,
                    [.. rows.Select(x => new StockWriteOffLineRequest(
                        x.VariantId, x.Quantity, x.Reason, x.Disposition, x.Batch!.Source.StockId,
                        string.IsNullOrWhiteSpace(x.Note) ? null : x.Note.Trim()))],
                    DateOnly.FromDateTime(BusinessDate.Date),
                    string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                    _idempotencyKey));

            _toast.Success(string.Format(L["write_off_created_fmt"], created.DocumentNumber));
            _idempotencyKey = null;
            CloseEditor();
            await RefreshAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private void NotifyEditor()
    {
        OnPropertyChanged(nameof(HasLines));
        OnPropertyChanged(nameof(TotalCost));
        OnPropertyChanged(nameof(ClaimTotal));
        OnPropertyChanged(nameof(HasMixedClaimBatches));
    }
}

/// Ro'yxatdagi bitta hujjat. Teskari hujjatning o'zi qaytarilmaydi, shu sahifada qaytarilgani
/// ko'ringan hujjat ham qaytarilmaydi — qolganida hakam server (BRAK-07).
public sealed class WriteOffRow(StockWriteOffDto document, bool isReversed, bool canWriteOff)
{
    public StockWriteOffDto Document { get; } = document;
    public bool IsReversed { get; } = isReversed;
    public bool IsReversal => Document.ReversesDocumentId is not null;
    public bool CanReverse => canWriteOff && !IsReversal && !IsReversed;
    public bool HasStatus => IsReversal || IsReversed;
    public string Status => HasStatus
        ? LocalizationManager.Instance[IsReversal ? "write_off_reversal" : "write_off_reversed"]
        : "";
}

/// Bitta chiqim qatori aynan bitta partiyadan chiqadi: omborchi qaysi jismoniy tovar
/// ketayotganini o'zi tanlaydi, dastur uni jimgina taqsimlab qo'ymaydi.
public partial class WriteOffEditorLine : ObservableObject
{
    private readonly Action _onChanged;

    public WriteOffEditorLine(
        long variantId,
        string productName,
        string unitName,
        IReadOnlyList<WriteOffBatchDto> batches,
        Action onChanged)
    {
        VariantId = variantId;
        ProductName = productName;
        UnitName = unitName;
        Batches = [.. batches.Select(x => new WriteOffBatchOption(x, Describe(x, unitName)))];
        _onChanged = onChanged;
        _batch = Batches[0];
    }

    /// Partiya nomida qaytarish imkoni ham yozilib turadi: omborchi ro'yxatni ochganda qaysi
    /// partiyani ta'minotchiga qaytarish mumkinligini shu yerda ko'radi (BRAK-03).
    private static string Describe(WriteOffBatchDto batch, string unitName)
    {
        var loc = LocalizationManager.Instance;
        var supplier = string.IsNullOrWhiteSpace(batch.SupplierName) ? loc["write_off_no_supplier"] : batch.SupplierName;
        var expiry = batch.ExpiredAt is { } date ? $" · {date:dd.MM.yyyy}" : "";
        var claim = loc[batch.SupplierAcceptsReturns ? "write_off_batch_claimable" : "write_off_batch_scrap_only"];
        return $"{batch.Quantity:0.###} {unitName} · {supplier}{expiry} · {claim}";
    }

    public long VariantId { get; }
    public string ProductName { get; }
    public string UnitName { get; }
    public IReadOnlyList<WriteOffBatchOption> Batches { get; }
    public IReadOnlyList<string> ReasonOptions => StockWriteOffsViewModel.Reasons;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineCost))]
    [NotifyPropertyChangedFor(nameof(Available))]
    [NotifyPropertyChangedFor(nameof(CanClaim))]
    [NotifyPropertyChangedFor(nameof(UnitCost))]
    private WriteOffBatchOption? _batch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineCost))]
    private decimal _quantity = 1m;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsClaim))]
    private string _disposition = "Scrap";

    [ObservableProperty] private string _reason = "Broken";
    [ObservableProperty] private string? _note;

    /// BRAK-04: ta'minotchiga qaytarish imkoni partiyaning ta'minotchisi sozlamasidan kelib
    /// chiqadi, shuning uchun variantlar ro'yxati har qatorda alohida bo'ladi.
    public IReadOnlyList<string> DispositionOptions => CanClaim ? ["Scrap", "SupplierClaim"] : ["Scrap"];

    public bool CanClaim => Batch?.Source.SupplierAcceptsReturns == true;
    public bool IsClaim => Disposition == "SupplierClaim";
    public decimal Available => Batch?.Source.Quantity ?? 0;
    public decimal UnitCost => Batch?.Source.PurchasePrice ?? 0;
    public decimal LineCost => Quantity * UnitCost;

    public bool HasMixedBatches =>
        Batches.Count > 1
        && Batches.Any(x => x.Source.SupplierAcceptsReturns)
        && Batches.Any(x => !x.Source.SupplierAcceptsReturns);

    partial void OnBatchChanged(WriteOffBatchOption? value)
    {
        OnPropertyChanged(nameof(DispositionOptions));
        // Qaytarishni qabul qilmaydigan partiyaga o'tilganda da'vo varianti o'z-o'zidan qolib
        // ketmasin — aks holda server rad etadigan qator saqlashga jo'nardi.
        if (!CanClaim && IsClaim) Disposition = "Scrap";
        OnPropertyChanged(nameof(IsClaim));
        _onChanged();
    }

    partial void OnQuantityChanged(decimal value) => _onChanged();
    partial void OnDispositionChanged(string value) => _onChanged();
}
