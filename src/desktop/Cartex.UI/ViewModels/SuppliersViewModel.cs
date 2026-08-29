using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.Suppliers;
using Cartex.Shared.Models.Supplies;
using Cartex.UI.Models;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;

namespace Cartex.UI.ViewModels;

public partial class SuppliersViewModel : ViewModelBase, ILoadable
{
    private readonly ISuppliersApi _api;
    private readonly ISuppliesApi _suppliesApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly AuthService _auth;
    private readonly ReferenceCache _cache;
    private readonly IBusinessApi _businessApi;
    private readonly IRatesApi _ratesApi;
    private long _editId;
    private long _detailSupplierId;
    private string _baseCurrency = "UZS";
    private string? _repayIdempotencyKey;

    public ObservableCollection<SupplierDto> Suppliers { get; } = [];
    public ObservableCollection<SupplierLedgerEntryDto> Ledger { get; } = [];
    public ObservableCollection<SupplyDto> Supplies { get; } = [];
    public PaginationState Paging { get; } = new();
    public PaginationState LedgerPaging { get; } = new();
    public PaginationState SuppliesPaging { get; } = new();

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private SupplierDto? _selectedSupplier;
    [ObservableProperty] private SupplierTotalsDto _totals = new(0, 0, 0);
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLedgerLoading;
    [ObservableProperty] private bool _isSuppliesLoading;
    [ObservableProperty] private bool _isSuppliesTab;

    /// Mijozlar sahifasidagi kabi: birinchi ekran ro'yxat, tanlangach o'sha yetkazib
    /// beruvchining profili to'liq sahifa bo'lib ochiladi.
    [ObservableProperty] private bool _isProfileOpen;
    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editName = "";
    [ObservableProperty] private string _editPhone = "";

    [ObservableProperty] private bool _isRepayOpen;
    [ObservableProperty] private decimal _repayAmount;
    [ObservableProperty] private PayMode? _repayMode;
    [ObservableProperty] private bool _isMulticurrency;
    [ObservableProperty] private string? _repayDebtCurrency;
    [ObservableProperty] private string? _repayPayCurrency;

    public ObservableCollection<PayMode> RepayModes { get; } = [];
    public ObservableCollection<string> RepayDebtCurrencies { get; } = [];
    public ObservableCollection<string> PayCurrencies { get; } = [];

    public bool IsModalOpen => IsEditOpen || IsRepayOpen;
    partial void OnIsEditOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsRepayOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));

    public bool IsEmpty => Suppliers.Count == 0;

    [RelayCommand]
    private void OpenProfile(SupplierDto supplier)
    {
        if (supplier is null) return;
        SelectedSupplier = supplier;
        IsProfileOpen = true;
    }

    [RelayCommand]
    private void CloseProfile()
    {
        IsProfileOpen = false;
        SelectedSupplier = null;
    }

    public bool HasSelection => SelectedSupplier is not null;
    private static readonly SupplierDto EmptySupplier = new(0, "", null, 0);
    public SupplierDto SelectedSupplierDisplay => SelectedSupplier ?? EmptySupplier;
    public bool CanCreate => _auth.HasPermission("suppliers.create");
    public bool CanEdit => _auth.HasPermission("suppliers.edit");
    public bool CanPay => _auth.HasPermission("suppliers.pay");
    public bool CanExport => _auth.HasPermission("reports.export");
    public bool CanViewSupplies => _auth.HasPermission("supplies.view");
    public bool SelectedIsAdvance => SelectedSupplier is { Payable: < 0 };
    public decimal SelectedPayableAmount => Math.Abs(SelectedSupplier?.Payable ?? 0);

    public string SelectedInitials => string.Concat(
        (SelectedSupplier?.Name ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(w => char.ToUpperInvariant(w[0])));

    private IReadOnlyList<PageShortcut>? _shortcuts;
    public IReadOnlyList<PageShortcut> Shortcuts => _shortcuts ??= CrudShortcuts(OpenCreateCommand, SaveCommand, () => IsEditOpen = false, () => IsEditOpen);

    public SuppliersViewModel(ISuppliersApi api, ISuppliesApi suppliesApi, IToastService toast, IBusyService busy, IExportService export, AuthService auth, ReferenceCache cache, IBusinessApi businessApi, IRatesApi ratesApi)
    {
        _api = api;
        _suppliesApi = suppliesApi;
        _toast = toast;
        _busy = busy;
        _export = export;
        _auth = auth;
        _cache = cache;
        _businessApi = businessApi;
        _ratesApi = ratesApi;
        _auth.LoggedOut += ResetState;
        Paging.Attach(LoadAsync);
        Paging.ConfigureSort([new(L["name"], "Name"), new(L["date"], "CreatedAt")]);
        LedgerPaging.Attach(() => _detailSupplierId == 0 ? Task.CompletedTask : LoadLedgerAsync(_detailSupplierId, NewLedgerToken()));
        SuppliesPaging.Attach(() => _detailSupplierId == 0 ? Task.CompletedTask : LoadSuppliesAsync(_detailSupplierId));
    }

    private void ResetState()
    {
        Debounce.Cancel(ref _searchCts);
        SelectedSupplier = null;
        Suppliers.Clear();
        Totals = new SupplierTotalsDto(0, 0, 0);
        IsEditOpen = false;
        IsRepayOpen = false;
        IsSuppliesTab = false;
        SearchText = "";
        OnPropertyChanged(nameof(IsEmpty));
    }

    private async Task EnsureCurrenciesAsync()
    {
        try
        {
            var business = await _cache.GetAsync(CacheKeys.Business, _businessApi.GetAsync);
            _baseCurrency = business.Currency;
            IsMulticurrency = business.PricingMulticurrency;
            PayCurrencies.Clear();
            PayCurrencies.Add(_baseCurrency);
            if (IsMulticurrency)
                foreach (var r in (await _cache.GetAsync(CacheKeys.Rates, _ratesApi.GetCurrentAsync)).OrderBy(r => r.Code))
                    PayCurrencies.Add(r.Code);
        }
        catch { }
    }

    private void RaisePermissions()
    {
        OnPropertyChanged(nameof(CanCreate));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanPay));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanViewSupplies));
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            var response = await _api.QueryAsync(QueryRequest.Create()
                .Page(0, 0)
                .Sort(Paging.SortBy, Paging.Descending)
                .Search(search)
                .Build());
            var all = response.Content ?? [];
            await _export.ExportAsync(L["suppliers"], all,
            [
                new(L["name"], s => s.Name),
                new(L["phone"], s => s.Phone),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    public async Task LoadAsync()
    {
        RaisePermissions();
        IsLoading = true;
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            var pagedTask = _api.QueryAsync(QueryRequest.Create()
                .Page(Paging.Page, Paging.PageSize)
                .Sort(Paging.SortBy, Paging.Descending)
                .Search(search)
                .Build());
            var totalsTask = _api.GetTotalsAsync(search);
            var paged = (await pagedTask).ToPaged();
            Suppliers.Clear();
            foreach (var s in paged.Items) Suppliers.Add(s);
            Paging.Apply(paged.Meta);
            Totals = await totalsTask;
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        finally { IsLoading = false; }
    }

    private CancellationTokenSource? _searchCts;

    partial void OnSearchTextChanged(string value)
    {
        var cts = Debounce.Restart(ref _searchCts);
        _ = DebouncedSearchAsync(cts.Token);
    }

    private async Task DebouncedSearchAsync(CancellationToken token)
    {
        try { await Task.Delay(300, token); } catch { return; }
        if (token.IsCancellationRequested) return;
        Paging.Page = 1;
        await LoadAsync();
    }

    private CancellationTokenSource? _ledgerCts;

    private CancellationToken NewLedgerToken()
    {
        return Debounce.Restart(ref _ledgerCts).Token;
    }

    partial void OnSelectedSupplierChanged(SupplierDto? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedSupplierDisplay));
        OnPropertyChanged(nameof(SelectedInitials));
        OnPropertyChanged(nameof(SelectedIsAdvance));
        OnPropertyChanged(nameof(SelectedPayableAmount));
        Ledger.Clear();
        Supplies.Clear();
        _detailSupplierId = value?.Id ?? 0;
        LedgerPaging.Page = 1;
        SuppliesPaging.Page = 1;
        if (value is null) { Debounce.Cancel(ref _ledgerCts); return; }
        _ = DebouncedLedgerAsync(value.Id, NewLedgerToken());
        if (IsSuppliesTab) _ = LoadSuppliesAsync(value.Id);
    }

    partial void OnIsSuppliesTabChanged(bool value)
    {
        if (value && _detailSupplierId != 0 && Supplies.Count == 0) _ = LoadSuppliesAsync(_detailSupplierId);
    }

    [RelayCommand]
    private void SetTab(string tab) => IsSuppliesTab = tab == "supplies";

    private async Task LoadSuppliesAsync(long id)
    {
        IsSuppliesLoading = true;
        try
        {
            var result = await _suppliesApi.QueryAsync(QueryRequest.Create()
                .Page(SuppliesPaging.Page, SuppliesPaging.PageSize)
                .Sort("CreatedAt", true)
                .With("supplierId", id)
                .Build());
            if (id != _detailSupplierId) return;
            var paged = result.ToPaged();
            Supplies.Clear();
            foreach (var s in paged.Items) Supplies.Add(s);
            SuppliesPaging.Apply(paged.Meta);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        finally { IsSuppliesLoading = false; }
    }

    private async Task DebouncedLedgerAsync(long id, CancellationToken token)
    {
        try { await Task.Delay(150, token); } catch { return; }
        await LoadLedgerAsync(id, token);
    }

    private async Task LoadLedgerAsync(long id, CancellationToken token)
    {
        IsLedgerLoading = true;
        try
        {
            var result = await _api.GetLedgerAsync(id, LedgerPaging.Page, LedgerPaging.PageSize, token);
            if (id != _detailSupplierId) return;
            var paged = result.ToPaged();
            Ledger.Clear();
            foreach (var e in paged.Items) Ledger.Add(e);
            LedgerPaging.Apply(paged.Meta);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        finally { if (!token.IsCancellationRequested) IsLedgerLoading = false; }
    }

    [RelayCommand]
    private void OpenCreate()
    {
        if (!CanCreate) return;
        IsNew = true;
        _editId = 0;
        EditName = "";
        EditPhone = "";
        IsEditOpen = true;
    }

    [RelayCommand]
    private void OpenEdit(SupplierDto supplier)
    {
        if (!CanEdit) return;
        IsNew = false;
        _editId = supplier.Id;
        EditName = supplier.Name;
        EditPhone = supplier.Phone ?? "";
        IsEditOpen = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private void OpenRepay()
    {
        if (!CanPay) return;
        if (SelectedSupplier is null) return;
        RepayAmount = 0;
        RepayModes.Clear();
        foreach (var mode in PayMode.All()) RepayModes.Add(mode);
        RepayMode = RepayModes[0];
        _ = EnsureCurrenciesAsync();
        RepayDebtCurrencies.Clear();
        foreach (var b in SelectedSupplier.PayableBalances) RepayDebtCurrencies.Add(b.Currency);
        if (RepayDebtCurrencies.Count == 0) RepayDebtCurrencies.Add(_baseCurrency);
        RepayDebtCurrency = RepayDebtCurrencies[0];
        RepayPayCurrency = RepayDebtCurrency;
        _repayIdempotencyKey = Guid.NewGuid().ToString("N");
        IsRepayOpen = true;
    }

    [RelayCommand]
    private void CancelRepay() => IsRepayOpen = false;

    [RelayCommand]
    private async Task RepayAsync()
    {
        if (!CanPay) return;
        if (SelectedSupplier is null || RepayAmount <= 0) { _toast.Error(L["error"]); return; }
        var id = SelectedSupplier.Id;
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.PayDebtAsync(id, new PaySupplierDebtRequest(RepayAmount, RepayMode?.Key ?? "Cash",
                    IsMulticurrency ? RepayDebtCurrency : null,
                    IsMulticurrency ? RepayPayCurrency : null,
                    null, _repayIdempotencyKey));
            IsRepayOpen = false;
            _toast.Success(L["success"]);
            _cache.Invalidate(CacheKeys.Suppliers);
            await LoadAsync();
            SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == id);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsNew ? !CanCreate : !CanEdit) return;
        if (string.IsNullOrWhiteSpace(EditName)) { _toast.Error(L["error"]); return; }
        var phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone.Trim();
        try
        {
            long targetId;
            using (_busy.Begin(L["loading"]))
            {
                if (IsNew)
                    targetId = await _api.CreateAsync(new CreateSupplierRequest(EditName.Trim(), phone));
                else
                {
                    await _api.UpdateAsync(_editId, new UpdateSupplierRequest(EditName.Trim(), phone));
                    targetId = _editId;
                }
            }
            IsEditOpen = false;
            _toast.Success(L["success"]);
            _cache.Invalidate(CacheKeys.Suppliers);
            await LoadAsync();
            SelectedSupplier = Suppliers.FirstOrDefault(s => s.Id == targetId);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
