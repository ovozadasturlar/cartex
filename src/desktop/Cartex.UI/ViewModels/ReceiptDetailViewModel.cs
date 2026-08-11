using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using Cartex.ApiClient;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Sales;
using Cartex.Shared.Models.TradeCases;
using Cartex.UI.Services;
using Cartex.UI.Views;

namespace Cartex.UI.ViewModels;

public enum ReceiptDialogResult
{
    Closed,
    NewSale,
    Returned,
    CustomerAssigned
}

public partial class ReceiptDetailViewModel : ViewModelBase, IDialogContext
{
    private readonly IReceiptApi _receiptApi;
    private readonly ISalesApi _salesApi;
    private readonly AuthService _auth;
    private readonly PrintDispatchService _print;
    private readonly IDialogService _dialog;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;

    private readonly string? _receiptToken;
    private long? _saleId;
    private long? _customerId;

    public ReceiptDetailViewModel(
        IReceiptApi receiptApi,
        ISalesApi salesApi,
        AuthService auth,
        PrintDispatchService print,
        IDialogService dialog,
        IToastService toast,
        IBusyService busy,
        ReceiptDto? preloadedReceipt = null,
        string? receiptToken = null,
        long? saleId = null,
        long? customerId = null,
        bool isPosCheckoutMode = false)
    {
        _receiptApi = receiptApi;
        _salesApi = salesApi;
        _auth = auth;
        _print = print;
        _dialog = dialog;
        _toast = toast;
        _busy = busy;

        _receiptToken = receiptToken ?? preloadedReceipt?.ReceiptToken;
        _saleId = saleId ?? preloadedReceipt?.SaleId;
        _customerId = customerId;
        IsPosCheckoutMode = isPosCheckoutMode;

        if (preloadedReceipt is not null)
            ApplyReceipt(preloadedReceipt);
    }

    [ObservableProperty] private ReceiptDto? _receipt;
    [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? _receiptQrCode;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isPosCheckoutMode;

    // --- Sub-panel states ---
    [ObservableProperty] private bool _isCustomerPickerOpen;
    [ObservableProperty] private bool _isCaseAttachOpen;

    // --- Customer Picker State ---
    public ObservableCollection<CustomerDto> CustomerResults { get; } = [];
    [ObservableProperty] private string _customerSearch = "";
    [ObservableProperty] private bool _isCreatingCustomer;
    [ObservableProperty] private string _newCustomerFirstName = "";
    [ObservableProperty] private string _newCustomerLastName = "";
    [ObservableProperty] private string _newCustomerPhone = "";
    public bool HasCustomerResults => CustomerResults.Count > 0;
    private CancellationTokenSource? _customerSearchCts;

    // --- Trade Case Attach State ---
    public ObservableCollection<TradeCaseListDto> OpenCases { get; } = [];
    [ObservableProperty] private bool _hasOpenCases;
    [ObservableProperty] private bool _isCreatingCase;
    [ObservableProperty] private string _newCaseTitle = "";
    [ObservableProperty] private string _newCaseSiteAddress = "";
    [ObservableProperty] private string _caseCustomerDisplay = "";

    public bool HasReceipt => Receipt is not null;
    public bool CanAttachCustomer => _auth.HasPermission("sales.edit") && Receipt is not null && string.IsNullOrEmpty(Receipt.CustomerName);
    public bool CanAttachCase => _auth.HasPermission("trade_cases.edit") && Receipt is not null && !Receipt.HasTradeCase;
    public bool CanReturnSale => _auth.HasPermission("sales.return") && !IsPosCheckoutMode && Receipt is not null;
    public bool CanPrint => _auth.HasPermission("printing.receipts.reprint") && Receipt is not null;
    public bool IsAnySubPanelOpen => IsCustomerPickerOpen || IsCaseAttachOpen;

    partial void OnIsCustomerPickerOpenChanged(bool value) => OnPropertyChanged(nameof(IsAnySubPanelOpen));
    partial void OnIsCaseAttachOpenChanged(bool value) => OnPropertyChanged(nameof(IsAnySubPanelOpen));

    public async Task InitAsync()
    {
        if (Receipt is not null) return;
        if (string.IsNullOrEmpty(_receiptToken)) return;

        try
        {
            IsLoading = true;
            using (_busy.Begin(L["loading"]))
            {
                var r = await _receiptApi.GetAsync(_receiptToken);
                ApplyReceipt(r);
            }
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            RequestClose?.Invoke(this, ReceiptDialogResult.Closed);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyReceipt(ReceiptDto r)
    {
        Receipt = r;
        if (_saleId is null or 0 && r.SaleId > 0)
            _saleId = r.SaleId;
        if ((_customerId is null or 0) && r.CustomerId is { } cid && cid > 0)
            _customerId = cid;

        var baseUrl = SettingsService.Instance.ApiBaseUrl?.TrimEnd('/');
        ReceiptQrCode = QrService.Generate($"{baseUrl}/r/{r.ReceiptToken}");
        OnPropertyChanged(nameof(HasReceipt));
        OnPropertyChanged(nameof(CanAttachCustomer));
        OnPropertyChanged(nameof(CanAttachCase));
        OnPropertyChanged(nameof(CanReturnSale));
        OnPropertyChanged(nameof(CanPrint));
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (Receipt is null || !CanPrint) return;
        try
        {
            await _print.PrintReceiptAsync(Receipt, true);
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    // ==========================================
    // CUSTOMER PICKER ACTIONS
    // ==========================================

    [RelayCommand]
    private async Task OpenCustomerPickerAsync()
    {
        if (Receipt is null || !CanAttachCustomer) return;
        IsCaseAttachOpen = false;
        IsCreatingCustomer = false;
        CustomerSearch = "";
        CustomerResults.Clear();
        IsCustomerPickerOpen = true;

        try
        {
            var customersApi = ServiceLocator.Resolve<ICustomersApi>();
            var list = await customersApi.GetAllAsync();
            foreach (var c in list.Take(20)) CustomerResults.Add(c);
            OnPropertyChanged(nameof(HasCustomerResults));
        }
        catch { }
    }

    partial void OnCustomerSearchChanged(string value)
    {
        _customerSearchCts?.Cancel();
        var cts = _customerSearchCts = new CancellationTokenSource();
        _ = DebouncedCustomerSearchAsync(cts.Token);
    }

    private async Task DebouncedCustomerSearchAsync(CancellationToken token)
    {
        try { await Task.Delay(250, token); } catch { return; }
        if (token.IsCancellationRequested) return;
        var query = CustomerSearch.Trim();
        CustomerResults.Clear();
        OnPropertyChanged(nameof(HasCustomerResults));
        try
        {
            var customersApi = ServiceLocator.Resolve<ICustomersApi>();
            var list = await customersApi.GetAllAsync(string.IsNullOrWhiteSpace(query) ? null : query);
            if (token.IsCancellationRequested) return;
            foreach (var c in list.Take(20)) CustomerResults.Add(c);
            OnPropertyChanged(nameof(HasCustomerResults));
        }
        catch { }
    }

    [RelayCommand]
    private void StartCreateCustomer()
    {
        IsCreatingCustomer = true;
        NewCustomerFirstName = CustomerSearch.Trim();
        NewCustomerLastName = "";
        NewCustomerPhone = "";
    }

    [RelayCommand]
    private void BackToCustomerList() => IsCreatingCustomer = false;

    [RelayCommand]
    private void CloseCustomerPicker()
    {
        IsCustomerPickerOpen = false;
        _customerSearchCts?.Cancel();
    }

    [RelayCommand]
    private async Task SelectCustomerAsync(CustomerDto customer)
    {
        if (customer is null) return;
        CloseCustomerPicker();
        await AssignCustomerToSaleAsync(customer.Id, $"{customer.FullName} {customer.LastName}".Trim());
    }

    [RelayCommand]
    private async Task SaveAndSelectCustomerAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCustomerFirstName))
        {
            _toast.Error(L["required_fields_hint"]);
            return;
        }

        try
        {
            CustomerDto created;
            var customersApi = ServiceLocator.Resolve<ICustomersApi>();
            using (_busy.Begin(L["loading"]))
            {
                var id = await customersApi.CreateAsync(new CreateCustomerRequest(
                    NewCustomerFirstName.Trim(),
                    string.IsNullOrWhiteSpace(NewCustomerPhone) ? null : NewCustomerPhone.Trim(),
                    null,
                    0,
                    null,
                    string.IsNullOrWhiteSpace(NewCustomerLastName) ? null : NewCustomerLastName.Trim()));
                created = await customersApi.GetByIdAsync(id);
            }
            CloseCustomerPicker();
            await AssignCustomerToSaleAsync(created.Id, $"{created.FullName} {created.LastName}".Trim());
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    private async Task AssignCustomerToSaleAsync(long customerId, string custFullName)
    {
        if (_saleId is not { } saleId || saleId <= 0) return;
        try
        {
            using (_busy.Begin(L["loading"]))
                await _salesApi.AssignCustomerAsync(saleId, customerId);

            _toast.Success(string.Format(L["customer_assigned_fmt"] ?? "Savdo mijozga ({0}) biriktirildi", custFullName));
            _customerId = customerId;
            if (Receipt is not null)
            {
                Receipt = Receipt with { CustomerId = customerId, CustomerName = custFullName };
                OnPropertyChanged(nameof(Receipt));
                OnPropertyChanged(nameof(CanAttachCustomer));
                OnPropertyChanged(nameof(CanAttachCase));
            }
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    // ==========================================
    // TRADE CASE ATTACH ACTIONS
    // ==========================================

    [RelayCommand]
    private async Task AttachCaseAsync()
    {
        if (Receipt is null || !CanAttachCase) return;
        if (_saleId is not { } saleId || saleId <= 0) return;

        if ((_customerId is null or 0) && Receipt.CustomerId is { } rcId && rcId > 0)
            _customerId = rcId;

        // If no customer attached yet, prompt to pick customer first
        if (string.IsNullOrEmpty(Receipt.CustomerName) || (_customerId is null or <= 0))
        {
            await OpenCustomerPickerAsync();
            return;
        }

        await LoadAndOpenCaseAttachPanelAsync(_customerId.Value, Receipt.CustomerName ?? "");
    }

    private async Task LoadAndOpenCaseAttachPanelAsync(long custId, string custName)
    {
        IsCustomerPickerOpen = false;
        CaseCustomerDisplay = custName;
        NewCaseTitle = $"{custName} — {DateTime.Today:dd.MM.yyyy}";
        NewCaseSiteAddress = "";
        IsCreatingCase = false;
        OpenCases.Clear();
        HasOpenCases = false;

        try
        {
            var tradeCasesApi = ServiceLocator.Resolve<ITradeCasesApi>();
            using (_busy.Begin(L["loading"]))
            {
                var open = await tradeCasesApi.GetAsync(customerId: custId, status: "Open", pageSize: 50);
                var pending = await tradeCasesApi.GetAsync(customerId: custId, status: "SettlementPending", pageSize: 50);
                foreach (var c in open.Concat(pending).OrderByDescending(x => x.UpdatedAt))
                    OpenCases.Add(c);
            }
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }

        HasOpenCases = OpenCases.Count > 0;
        if (!HasOpenCases)
            IsCreatingCase = true;

        IsCaseAttachOpen = true;
    }

    [RelayCommand]
    private void StartCreateCase() => IsCreatingCase = true;

    [RelayCommand]
    private void BackToCaseList() => IsCreatingCase = false;

    [RelayCommand]
    private void CloseCaseAttach() => IsCaseAttachOpen = false;

    [RelayCommand]
    private async Task SelectCaseAsync(TradeCaseListDto caseItem)
    {
        if (caseItem is null || _saleId is not { } saleId || saleId <= 0) return;
        CloseCaseAttach();
        await LinkSaleToCaseAsync(caseItem.Id, caseItem.CaseNumber, caseItem.Title, saleId);
    }

    [RelayCommand]
    private async Task CreateAndLinkCaseAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCaseTitle))
        {
            _toast.Error(L["required_fields_hint"]);
            return;
        }

        var branchService = ServiceLocator.Resolve<BranchContextService>();
        var warehouseId = branchService.CurrentWarehouseId ?? branchService.Warehouses.FirstOrDefault()?.Id;
        if (warehouseId is not { } wid || wid <= 0)
        {
            _toast.Warning(L["select_warehouse"]);
            return;
        }

        if (_customerId is not { } custId || custId <= 0)
        {
            _toast.Warning(L["case_customer_required"] ?? "Mijoz tanlanmagan.");
            return;
        }

        if (_saleId is not { } saleId || saleId <= 0) return;

        try
        {
            TradeCaseCreatedDto created;
            var tradeCasesApi = ServiceLocator.Resolve<ITradeCasesApi>();
            using (_busy.Begin(L["loading"]))
            {
                created = await tradeCasesApi.CreateAsync(new CreateTradeCaseRequest(
                    custId,
                    wid,
                    NewCaseTitle.Trim(),
                    string.IsNullOrWhiteSpace(NewCaseSiteAddress) ? null : NewCaseSiteAddress.Trim(),
                    IdempotencyKey: Guid.NewGuid().ToString("N")));
            }
            CloseCaseAttach();
            await LinkSaleToCaseAsync(created.Id, created.CaseNumber, NewCaseTitle.Trim(), saleId);
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    private async Task LinkSaleToCaseAsync(long caseId, string caseNumber, string? caseTitle, long saleId)
    {
        try
        {
            var tradeCasesApi = ServiceLocator.Resolve<ITradeCasesApi>();
            using (_busy.Begin(L["loading"]))
                await tradeCasesApi.LinkSaleAsync(caseId, saleId);

            _toast.Success(string.Format(L["case_linked_fmt"] ?? "Savdo {0}-sonli loyihaga muvaffaqiyatli biriktirildi", caseNumber));
            if (Receipt is not null)
            {
                Receipt = Receipt with
                {
                    TradeCaseId = caseId,
                    TradeCaseNumber = caseNumber,
                    TradeCaseTitle = caseTitle
                };
                OnPropertyChanged(nameof(Receipt));
                OnPropertyChanged(nameof(CanAttachCase));
            }
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    // ==========================================
    // OTHER ACTIONS
    // ==========================================

    [RelayCommand]
    private async Task ReturnSaleAsync()
    {
        if (Receipt is null || !CanReturnSale) return;
        var returnVm = new ReturnSaleViewModel(Receipt.SaleId, _salesApi, _toast, _busy);
        await returnVm.InitAsync();
        var returned = await _dialog.ShowAsync<ReturnSaleDialog, ReturnSaleViewModel, bool>(returnVm);
        if (returned)
            RequestClose?.Invoke(this, ReceiptDialogResult.Returned);
    }

    [RelayCommand]
    private void NewSale() => RequestClose?.Invoke(this, ReceiptDialogResult.NewSale);

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private void CloseModal() => RequestClose?.Invoke(this, ReceiptDialogResult.Closed);

    public void Close() => RequestClose?.Invoke(this, ReceiptDialogResult.Closed);
}
