using System.Collections.ObjectModel;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Partners;
using Cartex.Shared.Models.Sales;
using Cartex.UI.Models;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;

namespace Cartex.UI.ViewModels;

public sealed record MessageChannelOption(string Code, string Label);

public partial class CustomersViewModel : ViewModelBase, ILoadable
{
    private readonly ICustomersApi _api;
    private readonly ISalesApi _salesApi;
    private readonly IPartnersApi _partnersApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly AuthService _auth;
    private readonly IExportService _export;
    private readonly ReceiptDialogService _receiptDialog;
    private readonly ICustomerPaymentsApi _paymentsApi;
    private readonly ICustomerRefundsApi _refundsApi;
    private readonly PrintDispatchService _print;
    private readonly IDialogService _dialog;
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
    [ObservableProperty] private string _profileTab = "ledger";
    [ObservableProperty] private bool _isStatementLoading;
    [ObservableProperty] private DateTimeOffset _statementFrom = DateTimeOffset.Now.AddMonths(-1);
    [ObservableProperty] private DateTimeOffset _statementTo = DateTimeOffset.Now;
    [ObservableProperty] private CustomerStatementDto? _statement;
    [ObservableProperty] private bool _isProfileOpen;

    [ObservableProperty] private bool _isEditOpen;
    [ObservableProperty] private bool _isNew;
    [ObservableProperty] private string _editFullName = "";
    [ObservableProperty] private string _editLastName = "";
    [ObservableProperty] private string _editAddress = "";
    [ObservableProperty] private string _editNote = "";
    [ObservableProperty] private string _editPhone = "";
    [ObservableProperty] private string _editEmail = "";
    [ObservableProperty] private string _editCardBarcode = "";
    [ObservableProperty] private decimal _editDiscountPct;
    [ObservableProperty] private decimal? _editCreditLimit;
    [ObservableProperty] private bool _editNotificationsOptOut;
    [ObservableProperty] private decimal _editOpeningBalance;
    [ObservableProperty] private int _editOpeningKindIndex;
    [ObservableProperty] private string? _editOpeningCurrency;

    public ObservableCollection<string> OpeningKinds { get; } = [];

    [ObservableProperty] private bool _isRepayOpen;
    [ObservableProperty] private bool _isPayOutOpen;
    [ObservableProperty] private decimal _payOutAmount;
    [ObservableProperty] private bool _payOutViaCard;
    [ObservableProperty] private string _payOutNote = "";
    [ObservableProperty] private decimal _repayAmount;
    [ObservableProperty] private bool _repayViaCard;
    [ObservableProperty] private decimal _repayWriteOff;
    [ObservableProperty] private string _repayWriteOffReason = "";
    [ObservableProperty] private bool _canWriteOff;
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
        if (IsPublicityOpen) { IsPublicityOpen = false; return; }
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
        _ = LoadPartnerAsync(customer.Id);
    }

    [RelayCommand]
    private void CloseProfile()
    {
        IsProfileOpen = false;
        IsPublicityOpen = false;
        ShowPartner = false;
        SelectedCustomer = null;
    }

    private CustomerPartnerDto? _partner;

    [ObservableProperty] private bool _showPartner;
    [ObservableProperty] private bool _isPartner;

    public bool CanEditPartner => _auth.HasPermission("partners.edit");
    public bool CanOpenPublicity => IsPartner && _auth.HasPermission("partners.publish");

    partial void OnIsPartnerChanged(bool value) => OnPropertyChanged(nameof(CanOpenPublicity));

    /// The partner module can be switched off or out of this user's reach; when its state cannot be
    /// read there is nothing meaningful to offer, so the whole block stays hidden.
    private async Task LoadPartnerAsync(long customerId)
    {
        ShowPartner = false;
        _partner = null;
        IsPartner = false;
        if (!_auth.HasPermission("partners.view")) return;
        try
        {
            _partner = await _partnersApi.GetForCustomerAsync(customerId);
            IsPartner = _partner is { IsEnabled: true };
            ShowPartner = true;
        }
        catch { }
    }

    [RelayCommand]
    private async Task TogglePartnershipAsync()
    {
        if (!CanEditPartner || SelectedCustomer is not { } customer) return;
        try
        {
            using (_busy.Begin(L["loading"]))
                _partner = await _partnersApi.SetForCustomerAsync(customer.Id,
                    new SetCustomerPartnershipRequest(!IsPartner));
            IsPartner = _partner is { IsEnabled: true };
            _toast.Success(L["success"]);
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            OnPropertyChanged(nameof(IsPartner));
        }
    }

    private static readonly string[] ConsentCodes = ["NotAsked", "Granted", "Declined", "Withdrawn"];
    public ObservableCollection<string> ConsentOptions { get; } = [];

    [ObservableProperty] private bool _isPublicityOpen;
    [ObservableProperty] private int _consentIndex;
    [ObservableProperty] private bool _publicVisible;
    [ObservableProperty] private bool _publicPhoneVisible;
    [ObservableProperty] private string _publicDisplayName = "";
    [ObservableProperty] private string _publicAbout = "";

    /// The visibility switches only mean anything once a yes is on file; the server refuses the
    /// combination anyway, and hiding them here keeps the screen from suggesting otherwise.
    public bool ConsentGranted => ConsentIndex == 1;

    partial void OnConsentIndexChanged(int value)
    {
        OnPropertyChanged(nameof(ConsentGranted));
        if (value == 1) return;
        PublicVisible = false;
        PublicPhoneVisible = false;
    }

    [RelayCommand]
    private void OpenPublicity()
    {
        if (!CanOpenPublicity || _partner is not { } partner) return;
        ConsentIndex = Math.Max(0, Array.IndexOf(ConsentCodes, partner.PublicConsent));
        PublicVisible = partner.PublicVisible;
        PublicPhoneVisible = partner.PublicPhoneVisible;
        PublicDisplayName = partner.PublicDisplayName ?? "";
        PublicAbout = partner.PublicAbout ?? "";
        IsPublicityOpen = true;
    }

    [RelayCommand]
    private void CancelPublicity() => IsPublicityOpen = false;

    [RelayCommand]
    private async Task SavePublicityAsync()
    {
        if (!CanOpenPublicity || _partner is not { } partner || SelectedCustomer is not { } customer) return;
        try
        {
            using (_busy.Begin(L["loading"]))
                await _partnersApi.SetPublicityAsync(partner.PartnerId, new SetPartnerPublicityRequest(
                    ConsentCodes[Math.Clamp(ConsentIndex, 0, ConsentCodes.Length - 1)],
                    PublicVisible, PublicPhoneVisible, Trim(PublicDisplayName), Trim(PublicAbout)));
            IsPublicityOpen = false;
            _toast.Success(L["success"]);
            await LoadPartnerAsync(customer.Id);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private static string? Trim(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private decimal RateOf(string? code) => code is null || code == _baseCurrency ? 1m : _rates.GetValueOrDefault(code, 0m);

    public decimal RepayDebtTotal =>
        SelectedCustomer?.DebtBalances.FirstOrDefault(b => b.Currency == RepayDebtCurrency)?.Amount
        ?? SelectedDebtAmount;

    private decimal RepayAmountInDebtCurrency
    {
        get
        {
            var debtRate = RateOf(RepayDebtCurrency);
            var payRate = RateOf(RepayPayCurrency);
            return debtRate > 0 && payRate > 0 ? Math.Round(RepayAmount * payRate / debtRate, 2) : RepayAmount;
        }
    }

    public decimal RepayRemaining => Math.Max(0, RepayDebtTotal - RepayAmountInDebtCurrency);
    public bool RepayIsOverpay => RepayAmountInDebtCurrency > RepayDebtTotal;
    public decimal RepayOverpayAmount => Math.Max(0, RepayAmountInDebtCurrency - RepayDebtTotal);

    /// QARZ-20: haqdorlik o'chiq bo'lsa ortiqcha to'lov avansga aylanmaydi va server uni rad etadi —
    /// kassir buni saqlashdan oldin ko'rishi kerak, aks holda amal yarim yo'lda to'xtaydi.
    [ObservableProperty] private bool _allowCustomerCredit;
    public bool RepayOverpayBlocked => RepayIsOverpay && !AllowCustomerCredit;
    public bool RepayShowsAdvance => RepayIsOverpay && AllowCustomerCredit;
    private decimal RepayWriteOffInDebtCurrency
    {
        get
        {
            var debtRate = RateOf(RepayDebtCurrency);
            return debtRate > 0 ? Math.Round(RepayWriteOff / debtRate, 2) : RepayWriteOff;
        }
    }
    public decimal RepayDebtLeft => Math.Max(0, RepayRemaining - RepayWriteOffInDebtCurrency);

    private void NotifyRepayPreview()
    {
        OnPropertyChanged(nameof(RepayDebtTotal));
        OnPropertyChanged(nameof(RepayRemaining));
        OnPropertyChanged(nameof(RepayIsOverpay));
        OnPropertyChanged(nameof(RepayOverpayAmount));
        OnPropertyChanged(nameof(RepayOverpayBlocked));
        OnPropertyChanged(nameof(RepayShowsAdvance));
        OnPropertyChanged(nameof(RepayDebtLeft));
    }

    partial void OnRepayAmountChanged(decimal value) => NotifyRepayPreview();
    partial void OnRepayDebtCurrencyChanged(string? value) => NotifyRepayPreview();
    partial void OnRepayPayCurrencyChanged(string? value) => NotifyRepayPreview();
    partial void OnRepayWriteOffChanged(decimal value) => NotifyRepayPreview();

    [RelayCommand]
    private void ForgiveRest()
    {
        var debtRate = RateOf(RepayDebtCurrency);
        RepayWriteOff = debtRate > 0 ? Math.Round(RepayRemaining * debtRate, 2) : RepayRemaining;
    }

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
    public string EditTitle => L[IsNew ? "customer_new" : "customer_edit"];

    // QARZ-23: boshlang'ich qoldiq defterga yozadi — mijoz yaratish ruxsati yetarli emas.
    public bool CanEnterOpeningBalance => IsNew && _auth.HasPermission("customers.openingBalance");

    partial void OnIsNewChanged(bool value)
    {
        OnPropertyChanged(nameof(EditTitle));
        OnPropertyChanged(nameof(CanEnterOpeningBalance));
    }
    public bool IsModalOpen => IsEditOpen || IsMessageOpen || IsRepayOpen || IsPublicityOpen;
    partial void OnIsEditOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsMessageOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsRepayOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsPublicityOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    public bool CanCreate => _auth.HasPermission("customers.create");
    public bool CanEdit => _auth.HasPermission("customers.edit");
    public bool CanMessage => _auth.HasPermission("customers.message");
    public bool CanExport => _auth.HasPermission("reports.export");
    public bool CanViewSales => _auth.HasPermission("sales.view");
    public bool CanRepay => _auth.HasPermission("customers.receivePayment")
        && (SelectedCustomer?.DebtBalances?.Any(b => b.Amount > 0) ?? false);
    public bool SelectedIsCredit => SelectedCustomer is { DebtBalance: < 0 };

    /// Paying money out is a different door from taking it in: it needs its own permission, and
    /// the amount may exceed the customer's advance only when lending is switched on (QARZ-09).
    public bool CanPayOut => _auth.HasPermission("customers.refund") && SelectedCustomer is not null;
    public decimal PayOutAdvance => Math.Max(0, -(SelectedCustomer?.DebtBalance ?? 0));
    public decimal PayOutAsLoan => Math.Max(0, PayOutAmount - PayOutAdvance);
    public bool PayOutCreatesLoan => PayOutAsLoan > 0;

    partial void OnPayOutAmountChanged(decimal value)
    {
        OnPropertyChanged(nameof(PayOutAsLoan));
        OnPropertyChanged(nameof(PayOutCreatesLoan));
    }
    public decimal SelectedDebtAmount => Math.Abs(SelectedCustomer?.DebtBalance ?? 0);

    public string SelectedInitials => string.Concat(
        $"{SelectedCustomer?.FullName} {SelectedCustomer?.LastName}"
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(w => char.ToUpperInvariant(w[0])));

    public CustomersViewModel(ICustomersApi api, ISalesApi salesApi, IToastService toast, IBusyService busy, AuthService auth, IExportService export, IBusinessApi businessApi, IRatesApi ratesApi, ReferenceCache cache, ReceiptDialogService receiptDialog,
        ICustomerPaymentsApi paymentsApi, IDialogService dialog, PrintDispatchService print,
        ICustomerRefundsApi refundsApi, IPartnersApi partnersApi)
    {
        _partnersApi = partnersApi;
        _refundsApi = refundsApi;
        _print = print;
        _receiptDialog = receiptDialog;
        _paymentsApi = paymentsApi;
        _dialog = dialog;
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
        foreach (var code in ConsentCodes) ConsentOptions.Add(L[$"consent_{code.ToLowerInvariant()}"]);
        LedgerPaging.Attach(() => _ledgerCustomerId == 0 ? Task.CompletedTask : LoadLedgerAsync(_ledgerCustomerId, NewLedgerToken()));
        SalesPaging.Attach(() => _ledgerCustomerId == 0 ? Task.CompletedTask : LoadSalesAsync(_ledgerCustomerId));
    }

    [RelayCommand]
    private async Task OpenReceiptAsync(SaleDto sale)
    {
        if (sale is null || !CanViewSales) return;
        var result = await _receiptDialog.ShowAsync(sale);
        if (result is ReceiptDialogResult.Returned or ReceiptDialogResult.CustomerAssigned
            or ReceiptDialogResult.Corrected)
            await LoadSalesAsync(_ledgerCustomerId);
    }

    public bool CanVoidPayment => _auth.HasPermission("customer_payments.void");

    [RelayCommand]
    private Task OpenLedgerEntryAsync(CustomerLedgerEntryDto entry) => entry is null
        ? Task.CompletedTask
        : ShowTransactionAsync(entry.Date, entry.OperationType, entry.PaymentNumber,
            Math.Max(0, entry.Change), Math.Max(0, -entry.Change), entry.BalanceAfter,
            entry.Currency, entry.SaleId, entry.PaymentDocumentId);

    [RelayCommand]
    private Task OpenStatementEntryAsync(CustomerStatementEntryDto entry) => entry is null
        ? Task.CompletedTask
        : ShowTransactionAsync(entry.OccurredAt, entry.Summary, entry.DocumentNumber,
            entry.Debit, entry.Credit, entry.RunningBalance, entry.Currency, entry.SaleId,
            entry.Type == "CustomerPayment" ? entry.DocumentId : null);

    public bool CanBuildAct => _auth.HasPermission("customers.act");

    /// The act is picked from the statement the user is already looking at, so there is no second
    /// place where "which documents does this customer have" has to be answered.
    [RelayCommand]
    private async Task OpenConsolidatedActAsync()
    {
        if (!CanBuildAct || Statement is not { } statement) return;
        var vm = new ConsolidatedActViewModel(_api, _toast, _busy, ServiceLocator.Resolve<IPrinterService>(),
            _businessApi, statement.CustomerId, statement.CustomerName, statement.Timeline);
        await _dialog.ShowAsync<Views.ConsolidatedActDialog, ConsolidatedActViewModel, object?>(vm);
    }


    private async Task ShowTransactionAsync(
        DateTime occurredAt, string operation, string? documentNumber,
        decimal debit, decimal credit, decimal balance, string? currency,
        long? saleId, long? paymentDocumentId)
    {
        var vm = new TransactionDetailViewModel(_paymentsApi, _receiptDialog, _dialog, _toast, _busy, _auth, _print,
            occurredAt, operation, documentNumber, debit, credit, balance, currency, saleId, paymentDocumentId);
        var result = await _dialog.ShowAsync<Views.TransactionDetailDialog, TransactionDetailViewModel,
            TransactionDialogResult>(vm);
        if (result != TransactionDialogResult.Voided) return;
        await LoadLedgerAsync(_ledgerCustomerId, NewLedgerToken());
        await LoadStatementAsync();
        await LoadAsync();
    }

    private void ResetState()
    {
        Debounce.Cancel(ref _searchCts);
        SelectedCustomer = null;
        Customers.Clear();
        Totals = new CustomerTotalsDto(0, 0, 0);
        IsEditOpen = false;
        IsMessageOpen = false;
        IsRepayOpen = false;
        IsPublicityOpen = false;
        IsProfileOpen = false;
        ShowPartner = false;
        ProfileTab = "ledger";
        SearchText = "";
        OnPropertyChanged(nameof(IsEmpty));
    }

    public override void OnNavigatedFrom()
    {
        IsEditOpen = false;
        IsMessageOpen = false;
        IsRepayOpen = false;
        IsPublicityOpen = false;
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
        OnPropertyChanged(nameof(CanPayOut));
        OnPropertyChanged(nameof(CanEditPartner));
        OnPropertyChanged(nameof(CanOpenPublicity));
    }

    private static bool IsOfflineMode =>
        ServiceLocator.Resolve<OfflineSyncService>().ShouldUseOffline;

    private static OfflineStore Offline => ServiceLocator.Resolve<OfflineStore>();

    public async Task LoadAsync()
    {
        RaisePermissions();
        IsProfileOpen = false;
        IsLoading = true;
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            if (IsOfflineMode)
            {
                var baseCurrency = await Offline.GetMetaAsync("base_currency") ?? _baseCurrency;
                Customers.Clear();
                foreach (var c in await Offline.SearchCustomersAsync(search, 200))
                    Customers.Add(new CustomerDto(c.Id, c.FullName, null, null, c.Phone, null, c.CardBarcode,
                        c.DiscountPct, 0, c.DebtBalance, c.CreditLimit)
                    {
                        DebtBalances = c.DebtBalance > 0 ? [new CurrencyAmountDto(baseCurrency, c.DebtBalance)] : []
                    });
                OnPropertyChanged(nameof(IsEmpty));
                return;
            }
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

    partial void OnSelectedCustomerChanged(CustomerDto? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedCustomerDisplay));
        OnPropertyChanged(nameof(SelectedInitials));
        OnPropertyChanged(nameof(CanRepay));
        OnPropertyChanged(nameof(CanPayOut));
        OnPropertyChanged(nameof(SelectedIsCredit));
        OnPropertyChanged(nameof(SelectedDebtAmount));
        Ledger.Clear();
        Sales.Clear();
        _ledgerCustomerId = value?.Id ?? 0;
        LedgerPaging.Page = 1;
        SalesPaging.Page = 1;
        if (value is null) { Debounce.Cancel(ref _ledgerCts); return; }
        _ = DebouncedLedgerAsync(value.Id, NewLedgerToken());
        Statement = null;
        if (IsSalesTab) _ = LoadSalesAsync(value.Id);
        if (IsStatementTab) _ = LoadStatementAsync();
    }

    public bool IsLedgerTab => ProfileTab == "ledger";
    public bool IsSalesTab => ProfileTab == "sales";
    public bool IsStatementTab => ProfileTab == "statement";
    public bool CanViewStatement => _auth.HasPermission("statements.view");
    public bool CanExportStatement => _auth.HasPermission("statements.export");

    partial void OnProfileTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsLedgerTab));
        OnPropertyChanged(nameof(IsSalesTab));
        OnPropertyChanged(nameof(IsStatementTab));
        if (_ledgerCustomerId == 0) return;
        if (IsSalesTab && Sales.Count == 0) _ = LoadSalesAsync(_ledgerCustomerId);
        if (IsStatementTab && Statement is null) _ = LoadStatementAsync();
    }

    [RelayCommand]
    private void SetTab(string tab) => ProfileTab = tab;

    partial void OnStatementFromChanged(DateTimeOffset value) => _ = LoadStatementAsync();
    partial void OnStatementToChanged(DateTimeOffset value) => _ = LoadStatementAsync();

    [RelayCommand]
    private async Task LoadStatementAsync()
    {
        if (_ledgerCustomerId == 0 || !CanViewStatement) return;
        IsStatementLoading = true;
        try
        {
            Statement = await _api.GetStatementAsync(_ledgerCustomerId,
                new DateTimeOffset(StatementFrom.Date).UtcDateTime,
                new DateTimeOffset(StatementTo.Date.AddDays(1)).UtcDateTime);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
        finally { IsStatementLoading = false; }
    }

    [RelayCommand]
    private async Task ExportStatementAsync(string format)
    {
        if (_ledgerCustomerId == 0 || !CanExportStatement) return;
        // ExportButton offers Excel/Pdf/PdfPortrait/Csv; the statement endpoint only knows pdf and xlsx.
        var apiFormat = format switch
        {
            "Excel" => "xlsx",
            "Pdf" or "PdfPortrait" => "pdf",
            _ => null
        };
        if (apiFormat is null) { _toast.Warning(L["export_format_unsupported"]); return; }
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                using var content = await _api.ExportStatementAsync(_ledgerCustomerId, apiFormat, "both",
                    new DateTimeOffset(StatementFrom.Date).UtcDateTime,
                    new DateTimeOffset(StatementTo.Date.AddDays(1)).UtcDateTime);
                var name = $"{SelectedCustomerDisplay.FullName}-{StatementFrom:yyyyMMdd}-{StatementTo:yyyyMMdd}";
                await using var target = await ServiceLocator.Resolve<IFilePickerService>().SaveFileAsync(name, apiFormat);
                if (target is null) return;
                await content.CopyToAsync(target);
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

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
        EditNote = "";
        EditPhone = "";
        EditEmail = "";
        EditCardBarcode = "";
        EditDiscountPct = 0;
        EditCreditLimit = null;
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
        EditNote = customer.Note ?? "";
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
        var note = string.IsNullOrWhiteSpace(EditNote) ? null : EditNote.Trim();
        try
        {
            long targetId;
            using (_busy.Begin(L["loading"]))
            {
                if (IsNew)
                {
                    var opening = EditOpeningKindIndex == 1 ? -EditOpeningBalance : EditOpeningBalance;
                    targetId = await _api.CreateAsync(new CreateCustomerRequest(EditFullName.Trim(), phone, card, EditDiscountPct, email, lastName, address, EditCreditLimit, EditNotificationsOptOut,
                        opening, IsMulticurrency ? EditOpeningCurrency : null, EditLanguage, Note: note));
                }
                else
                {
                    await _api.UpdateAsync(_editId, new UpdateCustomerRequest(EditFullName.Trim(), phone, card, EditDiscountPct, email, lastName, address, EditCreditLimit, EditNotificationsOptOut, EditLanguage, note));
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

    /// Ro'yxatda kod emas, nom ko'rinadi; serverga esa baribir kod ketadi.
    public ObservableCollection<MessageChannelOption> MessageChannels { get; } = [];
    [ObservableProperty] private bool _isMessageOpen;
    [ObservableProperty] private string? _messageChannel;
    [ObservableProperty] private string _messageText = "";

    [RelayCommand]
    private void OpenMessage()
    {
        if (SelectedCustomer is null) return;
        MessageChannels.Clear();
        if (SelectedCustomer.HasTelegram) MessageChannels.Add(new("telegram", L["channel_telegram"]));
        if (!string.IsNullOrWhiteSpace(SelectedCustomer.Phone)) MessageChannels.Add(new("sms", L["channel_sms"]));
        if (!string.IsNullOrWhiteSpace(SelectedCustomer.Email)) MessageChannels.Add(new("email", L["channel_email"]));
        if (MessageChannels.Count == 0) { _toast.Warning(L["message_no_channel"]); return; }
        MessageChannel = MessageChannels[0].Code;
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
        RepayWriteOff = 0;
        RepayWriteOffReason = "";
        CanWriteOff = false;
        _ = LoadRepayPolicyAsync();
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

    private async Task LoadRepayPolicyAsync()
    {
        try
        {
            var policy = await _cache.GetAsync(CacheKeys.SalesPolicy,
                ServiceLocator.Resolve<ISettingsApi>().GetSalesPolicyAsync);
            AllowCustomerCredit = policy.AllowCustomerCredit;
            CanWriteOff = policy.AllowDebtWriteOff && _auth.HasPermission("customer_payments.writeOffDebt");
        }
        catch { }
        NotifyRepayPreview();
    }

    [RelayCommand]
    private void OpenPayOut()
    {
        if (!CanPayOut) return;
        PayOutAmount = 0;
        PayOutViaCard = false;
        PayOutNote = "";
        OnPropertyChanged(nameof(PayOutAdvance));
        IsPayOutOpen = true;
    }

    [RelayCommand]
    private void CancelPayOut() => IsPayOutOpen = false;

    [RelayCommand]
    private async Task PayOutAsync()
    {
        if (!CanPayOut || SelectedCustomer is not { } customer || PayOutAmount <= 0)
        {
            _toast.Error(L["error"]);
            return;
        }
        var id = customer.Id;
        try
        {
            CustomerRefundCreatedDto created;
            using (_busy.Begin(L["loading"]))
                created = await _refundsApi.CreateAsync(new CreateCustomerRefundRequest(
                    id,
                    null,
                    [new CustomerRefundTenderRequest(PayOutViaCard ? "Card" : "Cash", _baseCurrency, PayOutAmount)],
                    Note: string.IsNullOrWhiteSpace(PayOutNote) ? null : PayOutNote.Trim(),
                    IdempotencyKey: Guid.NewGuid().ToString("N")));
            IsPayOutOpen = false;
            // QARZ-22: siyosat ogohlantirishga qo'yilgan bo'lsa, qarz limitidan oshgani ko'rinib tursin.
            if (created.Warnings?.Contains("credit_limit_exceeded") == true)
                _toast.Warning(L["credit_limit_exceeded_warning"]);
            _toast.Success(L["success"]);
            await LoadAsync();
            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == id);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task RepayAsync()
    {
        if (!_auth.HasPermission("customers.receivePayment")) return;
        if (SelectedCustomer is null || RepayAmount + RepayWriteOff <= 0) { _toast.Error(L["error"]); return; }
        // QARZ-20: server ham rad etadi, lekin kassir sababni saqlashdan oldin ko'rgani ma'qul.
        if (RepayOverpayBlocked)
        {
            _toast.Error(L["repay_overpay_blocked"]);
            return;
        }
        if (RepayWriteOff > 0 && string.IsNullOrWhiteSpace(RepayWriteOffReason))
        {
            _toast.Error(L["write_off_reason_required"]);
            return;
        }
        var id = SelectedCustomer.Id;
        if (IsOfflineMode)
        {
            if (RepayWriteOff > 0)
            {
                _toast.Warning(L["offline_pos_limited"]);
                return;
            }
            if (!SettingsService.Instance.OfflineAllowPayments)
            {
                _toast.Warning(L["offline_pos_limited"]);
                return;
            }
            var baseCurrency = await Offline.GetMetaAsync("base_currency") ?? _baseCurrency;
            if (IsMulticurrency &&
                ((RepayDebtCurrency ?? baseCurrency) != baseCurrency || (RepayPayCurrency ?? baseCurrency) != baseCurrency))
            {
                _toast.Warning(L["offline_pos_limited"]);
                return;
            }
            try
            {
                await ServiceLocator.Resolve<OfflineSyncService>().EnqueuePaymentAsync(new OfflinePaymentDraft(
                    id, ServiceLocator.Resolve<BranchContextService>().CurrentBranchId, RepayAmount, RepayViaCard));
            }
            catch (Exception ex)
            {
                _toast.Error(ApiErrors.Describe(ex));
                return;
            }
            IsRepayOpen = false;
            _toast.Success(L["offline_payment_queued"]);
            await LoadAsync();
            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == id);
            return;
        }
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.RepayDebtAsync(id, new RepayDebtRequest(RepayAmount, RepayViaCard,
                    IsMulticurrency ? RepayDebtCurrency : null,
                    IsMulticurrency ? RepayPayCurrency : null,
                    Guid.NewGuid().ToString("N"),
                    RepayWriteOff,
                    Trim(RepayWriteOffReason)));
            IsRepayOpen = false;
            _toast.Success(L["success"]);
            await LoadAsync();
            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == id);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
