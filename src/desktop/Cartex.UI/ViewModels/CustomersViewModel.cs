using System.Collections.ObjectModel;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Sales;
using Cartex.UI.Models;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;

namespace Cartex.UI.ViewModels;

public partial class CustomersViewModel : ViewModelBase, ILoadable
{
    private readonly ICustomersApi _api;
    private readonly ISalesApi _salesApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly AuthService _auth;
    private readonly IExportService _export;
    private long _editId;

    public ObservableCollection<CustomerDto> Customers { get; } = [];
    public ObservableCollection<CustomerLedgerEntryDto> Ledger { get; } = [];
    public ObservableCollection<SaleDto> Sales { get; } = [];
    public PaginationState Paging { get; } = new();
    public PaginationState LedgerPaging { get; } = new();
    public PaginationState SalesPaging { get; } = new();
    private long _ledgerCustomerId;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private CustomerDto? _selectedCustomer;
    [ObservableProperty] private CustomerTotalsDto _totals = new(0, 0, 0);
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isLedgerLoading;
    [ObservableProperty] private bool _isSalesLoading;
    [ObservableProperty] private bool _isSalesTab;
    [ObservableProperty] private bool _isProfileOpen;

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editFullName = "";
    [ObservableProperty] private string _editLastName = "";
    [ObservableProperty] private string _editAddress = "";
    [ObservableProperty] private string _editPhone = "";
    [ObservableProperty] private string _editEmail = "";
    [ObservableProperty] private string _editCardBarcode = "";
    [ObservableProperty] private decimal _editDiscountPct;
    [ObservableProperty] private decimal _editCreditLimit;
    [ObservableProperty] private bool _editNotificationsOptOut;
    [ObservableProperty] private decimal _editOpeningBalance;
    [ObservableProperty] private int _editOpeningKindIndex;
    [ObservableProperty] private string? _editOpeningCurrency;

    public ObservableCollection<string> OpeningKinds { get; } = [];

    [ObservableProperty] private bool _isRepayOpen;
    [ObservableProperty] private decimal _repayAmount;
    [ObservableProperty] private bool _repayViaCard;
    [ObservableProperty] private bool _isMulticurrency;
    [ObservableProperty] private string? _repayDebtCurrency;
    [ObservableProperty] private string? _repayPayCurrency;

    public ObservableCollection<string> RepayDebtCurrencies { get; } = [];
    public ObservableCollection<string> PayCurrencies { get; } = [];
    private string _baseCurrency = "UZS";
    private readonly Dictionary<string, decimal> _rates = [];

    private IBusinessApi _businessApi = null!;
    private IRatesApi _ratesApi = null!;
    private ReferenceCache _cache = null!;

    private async Task EnsureCurrenciesAsync()
    {
        try
        {
            var business = await _cache.GetAsync(CacheKeys.Business, _businessApi.GetAsync);
            _baseCurrency = business.Currency;
            IsMulticurrency = business.SalesMulticurrency;
            PayCurrencies.Clear();
            PayCurrencies.Add(_baseCurrency);
            _rates.Clear();
            if (IsMulticurrency)
                foreach (var r in (await _cache.GetAsync(CacheKeys.Rates, _ratesApi.GetCurrentAsync)).OrderBy(r => r.Code))
                {
                    PayCurrencies.Add(r.Code);
                    _rates[r.Code] = r.Rate;
                }
        }
        catch { }
    }

    private IReadOnlyList<PageShortcut>? _pageShortcuts;

    public IReadOnlyList<PageShortcut> Shortcuts => _pageShortcuts ??=
    [
        new(Key.N, KeyModifiers.Control, "shortcut_new", () => OpenCreateCommand.Execute(null), WorksInText: true),
        new(Key.Escape, KeyModifiers.None, "shortcut_close", HandleEscape, WorksInText: true),
    ];

    private void HandleEscape()
    {
        if (IsRepayOpen) { IsRepayOpen = false; return; }
        if (IsMessageOpen) { IsMessageOpen = false; return; }
        if (IsEditOpen) { IsEditOpen = false; return; }
        if (IsProfileOpen) CloseProfile();
    }

    [RelayCommand]
    private void OpenProfile(CustomerDto customer)
    {
        SelectedCustomer = customer;
        IsProfileOpen = true;
    }

    [RelayCommand]
    private void CloseProfile()
    {
        IsProfileOpen = false;
        SelectedCustomer = null;
    }

    private decimal RateOf(string? code) => code is null || code == _baseCurrency ? 1m : _rates.GetValueOrDefault(code, 0m);

    [RelayCommand]
    private void FillRepayAmount()
    {
        var balance = SelectedCustomer?.DebtBalances.FirstOrDefault(b => b.Currency == RepayDebtCurrency && b.Amount > 0);
        var debt = balance?.Amount ?? SelectedDebtAmount;
        var debtRate = RateOf(balance?.Currency ?? _baseCurrency);
        var payRate = RateOf(RepayPayCurrency);
        RepayAmount = payRate > 0 && debtRate > 0 ? Math.Round(debt * debtRate / payRate, 2) : debt;
    }

    public bool IsEmpty => Customers.Count == 0;
    public bool HasSelection => SelectedCustomer is not null;
    private static readonly CustomerDto EmptyCustomer = new(0, "", null, null, null, null, null, 0, 0, 0, 0);
    public CustomerDto SelectedCustomerDisplay => SelectedCustomer ?? EmptyCustomer;
    public bool IsModalOpen => IsEditOpen || IsMessageOpen || IsRepayOpen;
    partial void OnIsEditOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsMessageOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsRepayOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    public bool CanCreate => _auth.HasPermission("customers.create");
    public bool CanEdit => _auth.HasPermission("customers.edit");
    public bool CanMessage => _auth.HasPermission("customers.message");
    public bool CanExport => _auth.HasPermission("reports.export");
    public bool CanViewSales => _auth.HasPermission("sales.view");
    public bool CanRepay => _auth.HasPermission("customers.receivePayment")
        && (SelectedCustomer?.DebtBalances?.Any(b => b.Amount > 0) ?? false);
    public bool SelectedIsCredit => SelectedCustomer is { DebtBalance: < 0 };
    public decimal SelectedDebtAmount => Math.Abs(SelectedCustomer?.DebtBalance ?? 0);

    public string SelectedInitials => string.Concat(
        $"{SelectedCustomer?.FullName} {SelectedCustomer?.LastName}"
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(w => char.ToUpperInvariant(w[0])));

    public CustomersViewModel(ICustomersApi api, ISalesApi salesApi, IToastService toast, IBusyService busy, AuthService auth, IExportService export, IBusinessApi businessApi, IRatesApi ratesApi, ReferenceCache cache)
    {
        _businessApi = businessApi;
        _ratesApi = ratesApi;
        _cache = cache;
        _api = api;
        _salesApi = salesApi;
        _toast = toast;
        _busy = busy;
        _auth = auth;
        _export = export;
        _auth.LoggedOut += ResetState;
        Paging.Attach(LoadAsync);
        Paging.ConfigureSort([new(L["full_name"], "FullName"), new(L["date"], "CreatedAt")]);
        LedgerPaging.Attach(() => _ledgerCustomerId == 0 ? Task.CompletedTask : LoadLedgerAsync(_ledgerCustomerId, NewLedgerToken()));
        SalesPaging.Attach(() => _ledgerCustomerId == 0 ? Task.CompletedTask : LoadSalesAsync(_ledgerCustomerId));
    }

    private void ResetState()
    {
        _searchCts?.Cancel();
        SelectedCustomer = null;
        Customers.Clear();
        Totals = new CustomerTotalsDto(0, 0, 0);
        IsEditOpen = false;
        IsMessageOpen = false;
        IsRepayOpen = false;
        IsProfileOpen = false;
        IsSalesTab = false;
        SearchText = "";
        OnPropertyChanged(nameof(IsEmpty));
    }

    public override void OnNavigatedFrom()
    {
        IsEditOpen = false;
        IsMessageOpen = false;
        IsRepayOpen = false;
        IsProfileOpen = false;
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            var all = await _api.GetAllAsync(search);
            await _export.ExportAsync(L["customers"], all,
            [
                new(L["full_name"], c => c.FullName),
                new(L["phone"], c => c.Phone),
                new(L["email"], c => c.Email),
                new(L["card_barcode"], c => c.CardBarcode),
                new(L["discount_pct"], c => c.DiscountPct),
                new(L["cashback_balance"], c => c.CashbackBalance),
                new(L["debt"], c => c.DebtBalance),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private void RaisePermissions()
    {
        OnPropertyChanged(nameof(CanCreate));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanMessage));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanViewSales));
        OnPropertyChanged(nameof(CanRepay));
    }

    public async Task LoadAsync()
    {
        RaisePermissions();
        IsProfileOpen = false;
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
            Customers.Clear();
            foreach (var c in paged.Items) Customers.Add(c);
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
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
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
        _ledgerCts?.Cancel();
        _ledgerCts?.Dispose();
        return (_ledgerCts = new CancellationTokenSource()).Token;
    }

    partial void OnSelectedCustomerChanged(CustomerDto? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedCustomerDisplay));
        OnPropertyChanged(nameof(SelectedInitials));
        OnPropertyChanged(nameof(CanRepay));
        OnPropertyChanged(nameof(SelectedIsCredit));
        OnPropertyChanged(nameof(SelectedDebtAmount));
        Ledger.Clear();
        Sales.Clear();
        _ledgerCustomerId = value?.Id ?? 0;
        LedgerPaging.Page = 1;
        SalesPaging.Page = 1;
        if (value is null) { _ledgerCts?.Cancel(); return; }
        _ = DebouncedLedgerAsync(value.Id, NewLedgerToken());
        if (IsSalesTab) _ = LoadSalesAsync(value.Id);
    }

    partial void OnIsSalesTabChanged(bool value)
    {
        if (value && _ledgerCustomerId != 0 && Sales.Count == 0) _ = LoadSalesAsync(_ledgerCustomerId);
    }

    [RelayCommand]
    private void SetTab(string tab) => IsSalesTab = tab == "sales";

    private async Task LoadSalesAsync(long id)
    {
        IsSalesLoading = true;
        try
        {
            var result = await _salesApi.QueryAsync(QueryRequest.Create()
                .Page(SalesPaging.Page, SalesPaging.PageSize)
                .Sort("CreatedAt", true)
                .With("customerId", id)
                .Build());
            if (id != _ledgerCustomerId) return;
            var paged = result.ToPaged();
            Sales.Clear();
            foreach (var s in paged.Items) Sales.Add(s);
            SalesPaging.Apply(paged.Meta);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        finally { IsSalesLoading = false; }
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
            if (id != _ledgerCustomerId) return;
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
    private async Task OpenCreateAsync()
    {
        if (!CanCreate) return;
        IsNew = true;
        _editId = 0;
        EditFullName = "";
        EditLastName = "";
        EditAddress = "";
        EditPhone = "";
        EditEmail = "";
        EditCardBarcode = "";
        EditDiscountPct = 0;
        EditCreditLimit = 0;
        EditNotificationsOptOut = false;
        EditLanguage = "uz-latn";
        await EnsureCurrenciesAsync();
        EditOpeningBalance = 0;
        EditOpeningKindIndex = 0;
        OpeningKinds.Clear();
        OpeningKinds.Add(L["opening_kind_debt"]);
        OpeningKinds.Add(L["opening_kind_credit"]);
        EditOpeningCurrency = _baseCurrency;
        IsEditOpen = true;
    }

    [RelayCommand]
    private void OpenEdit(CustomerDto customer)
    {
        if (!CanEdit) return;
        IsNew = false;
        _editId = customer.Id;
        EditFullName = customer.FullName;
        EditLastName = customer.LastName ?? "";
        EditAddress = customer.Address ?? "";
        EditPhone = customer.Phone ?? "";
        EditEmail = customer.Email ?? "";
        EditCardBarcode = customer.CardBarcode ?? "";
        EditDiscountPct = customer.DiscountPct;
        EditCreditLimit = customer.CreditLimit;
        EditNotificationsOptOut = customer.NotificationsOptOut;
        EditLanguage = customer.PreferredLanguage ?? "uz-latn";
        IsEditOpen = true;
    }

    public string[] CustomerLanguages { get; } = ["uz-latn", "uz-cyrl", "ru", "en"];
    [ObservableProperty] private string _editLanguage = "uz-latn";

    [RelayCommand]
    private void CancelEdit() => IsEditOpen = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsNew ? !CanCreate : !CanEdit) return;
        if (string.IsNullOrWhiteSpace(EditFullName) || string.IsNullOrWhiteSpace(EditPhone)) { _toast.Error(L["required_fields_hint"]); return; }
        var phone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone.Trim();
        var email = string.IsNullOrWhiteSpace(EditEmail) ? null : EditEmail.Trim();
        var card = string.IsNullOrWhiteSpace(EditCardBarcode) ? null : EditCardBarcode.Trim();
        var lastName = string.IsNullOrWhiteSpace(EditLastName) ? null : EditLastName.Trim();
        var address = string.IsNullOrWhiteSpace(EditAddress) ? null : EditAddress.Trim();
        try
        {
            long targetId;
            using (_busy.Begin(L["loading"]))
            {
                if (IsNew)
                {
                    var opening = EditOpeningKindIndex == 1 ? -EditOpeningBalance : EditOpeningBalance;
                    targetId = await _api.CreateAsync(new CreateCustomerRequest(EditFullName.Trim(), phone, card, EditDiscountPct, email, lastName, address, EditCreditLimit, EditNotificationsOptOut,
                        opening, IsMulticurrency ? EditOpeningCurrency : null, EditLanguage));
                }
                else
                {
                    await _api.UpdateAsync(_editId, new UpdateCustomerRequest(EditFullName.Trim(), phone, card, EditDiscountPct, email, lastName, address, EditCreditLimit, EditNotificationsOptOut, EditLanguage));
                    targetId = _editId;
                }
            }
            IsEditOpen = false;
            _toast.Success(L["success"]);
            await LoadAsync();
            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == targetId);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    public ObservableCollection<string> MessageChannels { get; } = [];
    [ObservableProperty] private bool _isMessageOpen;
    [ObservableProperty] private string? _messageChannel;
    [ObservableProperty] private string _messageText = "";

    [RelayCommand]
    private void OpenMessage()
    {
        if (SelectedCustomer is null) return;
        MessageChannels.Clear();
        if (SelectedCustomer.HasTelegram) MessageChannels.Add("telegram");
        if (!string.IsNullOrWhiteSpace(SelectedCustomer.Phone)) MessageChannels.Add("sms");
        if (!string.IsNullOrWhiteSpace(SelectedCustomer.Email)) MessageChannels.Add("email");
        if (MessageChannels.Count == 0) { _toast.Warning(L["message_no_channel"]); return; }
        MessageChannel = MessageChannels[0];
        MessageText = "";
        IsMessageOpen = true;
    }

    [RelayCommand]
    private void CancelMessage() => IsMessageOpen = false;

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (SelectedCustomer is null || MessageChannel is null || string.IsNullOrWhiteSpace(MessageText)) { _toast.Error(L["error"]); return; }
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.SendMessageAsync(SelectedCustomer.Id, new SendCustomerMessageRequest(MessageChannel, MessageText.Trim()));
            IsMessageOpen = false;
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void OpenRepay()
    {
        if (!CanRepay) return;
        if (SelectedCustomer is null) return;
        RepayAmount = 0;
        RepayViaCard = false;
        _ = EnsureCurrenciesAsync();
        RepayDebtCurrencies.Clear();
        foreach (var b in SelectedCustomer.DebtBalances) RepayDebtCurrencies.Add(b.Currency);
        if (RepayDebtCurrencies.Count == 0) RepayDebtCurrencies.Add(_baseCurrency);
        RepayDebtCurrency = RepayDebtCurrencies[0];
        RepayPayCurrency = RepayDebtCurrency;
        IsRepayOpen = true;
    }

    [RelayCommand]
    private void CancelRepay() => IsRepayOpen = false;

    [RelayCommand]
    private async Task RepayAsync()
    {
        if (!_auth.HasPermission("customers.receivePayment")) return;
        if (SelectedCustomer is null || RepayAmount <= 0) { _toast.Error(L["error"]); return; }
        var id = SelectedCustomer.Id;
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.RepayDebtAsync(id, new RepayDebtRequest(RepayAmount, RepayViaCard,
                    IsMulticurrency ? RepayDebtCurrency : null,
                    IsMulticurrency ? RepayPayCurrency : null));
            IsRepayOpen = false;
            _toast.Success(L["success"]);
            await LoadAsync();
            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == id);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
