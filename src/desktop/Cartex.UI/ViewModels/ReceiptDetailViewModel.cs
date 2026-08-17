using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using Cartex.ApiClient;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Sales;
using Cartex.UI.Services;
using Cartex.UI.Views;

namespace Cartex.UI.ViewModels;

public enum ReceiptDialogResult
{
    Closed,
    NewSale,
    Returned,
    CustomerAssigned,
    Corrected
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
    private bool _allowReturnOnVoidedSale;

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
    [ObservableProperty] private string? _loadError;
    [ObservableProperty] private bool _isPosCheckoutMode;

    // --- Sub-panel states ---
    [ObservableProperty] private bool _isCustomerPickerOpen;

    // --- Customer Picker State ---
    public ObservableCollection<CustomerDto> CustomerResults { get; } = [];
    [ObservableProperty] private string _customerSearch = "";
    [ObservableProperty] private bool _isCreatingCustomer;
    [ObservableProperty] private string _newCustomerFirstName = "";
    [ObservableProperty] private string _newCustomerLastName = "";
    [ObservableProperty] private string _newCustomerPhone = "";
    public bool HasCustomerResults => CustomerResults.Count > 0;
    private CancellationTokenSource? _customerSearchCts;

    public bool HasReceipt => Receipt is not null;
    public bool HasLoadError => !string.IsNullOrWhiteSpace(LoadError);
    public bool CanAttachCustomer => (_auth.HasPermission("sales.assignCustomer") || _auth.HasPermission("sales.create") || _auth.HasPermission("customers.create") || _auth.HasPermission("customers.edit")) && Receipt is not null && string.IsNullOrEmpty(Receipt.CustomerName);
    /// Bekor qilingan savdo allaqachon ortga qaytarilgan: uni na qaytarish, na qayta tuzatish
    /// mumkin (QAYT-08). Server ham rad etadi, tugma esa umuman ko'rinmasligi kerak.
    public bool IsSaleOpen => Receipt is null || Receipt.Status is "Completed" or "PartialReturn";

    /// Bekor qilinganini qayta bekor qilishning ma'nosi yo'q, shuning uchun tuzatish siyosatdan
    /// qat'i nazar yopiq. Qaytarishni esa do'kon o'z siyosati bilan ocha oladi.
    private bool IsReturnable => IsSaleOpen || (Receipt?.Status == "Voided" && _allowReturnOnVoidedSale);

    public bool CanReturnSale => _auth.HasPermission("returns.create") && !IsPosCheckoutMode && Receipt is not null && IsReturnable;
    public bool CanPrint => (_auth.HasPermission("printing.receipts.print") || _auth.HasPermission("printing.receipts.reprint")) && Receipt is not null;
    public bool CanCorrect => _auth.HasPermission("sales.void") && _saleId is > 0 && Receipt is not null && IsSaleOpen;
    public bool IsAnySubPanelOpen => IsCustomerPickerOpen;

    partial void OnIsCustomerPickerOpenChanged(bool value) => OnPropertyChanged(nameof(IsAnySubPanelOpen));

    public async Task InitAsync()
    {
        if (Receipt is not null) return;
        if (string.IsNullOrEmpty(_receiptToken))
        {
            LoadError = L["receipt_missing_token"];
            OnPropertyChanged(nameof(HasLoadError));
            return;
        }

        try
        {
            IsLoading = true;
            LoadError = null;
            OnPropertyChanged(nameof(HasLoadError));
            var r = await _receiptApi.GetAsync(_receiptToken);
            ApplyReceipt(r);
        }
        catch (Exception ex)
        {
            // The dialog stays open with a retry action: a failed fetch must never look
            // like the button did nothing.
            LoadError = ApiErrors.Describe(ex);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasLoadError));
        }
    }

    [RelayCommand]
    private Task RetryAsync()
    {
        Receipt = null;
        return InitAsync();
    }

    private void ApplyReceipt(ReceiptDto r)
    {
        Receipt = r;
        if (_saleId is null or 0 && r.SaleId > 0)
            _saleId = r.SaleId;
        if ((_customerId is null or 0) && r.CustomerId is { } cid && cid > 0)
            _customerId = cid;

        if (r.Status == "Voided") _ = LoadReturnPolicyAsync();

        var baseUrl = SettingsService.Instance.ApiBaseUrl?.TrimEnd('/');
        ReceiptQrCode = QrService.Generate($"{baseUrl}/r/{r.ReceiptToken}");
        OnPropertyChanged(nameof(HasReceipt));
        OnPropertyChanged(nameof(CanAttachCustomer));
        OnPropertyChanged(nameof(IsSaleOpen));
        OnPropertyChanged(nameof(CanReturnSale));
        OnPropertyChanged(nameof(CanPrint));
        OnPropertyChanged(nameof(CanCorrect));
    }

    /// Faqat bekor qilingan chek ochilganda so'raladi — oddiy chekda bu savolning ma'nosi yo'q.
    private async Task LoadReturnPolicyAsync()
    {
        try
        {
            _allowReturnOnVoidedSale = (await ServiceLocator.Resolve<ReferenceCache>()
                .GetAsync(CacheKeys.SalesPolicy, ServiceLocator.Resolve<ISettingsApi>().GetSalesPolicyAsync))
                .AllowReturnOnVoidedSale;
            OnPropertyChanged(nameof(CanReturnSale));
        }
        catch { }
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
    private async Task CorrectSaleAsync()
    {
        if (!CanCorrect || _saleId is not { } saleId) return;
        var reason = await _dialog.PromptAsync(L["correct_sale"], L["correct_sale_confirm"], L["correct_sale_reason"]);
        if (string.IsNullOrWhiteSpace(reason)) return;

        try
        {
            using (_busy.Begin(L["loading"]))
                await _salesApi.VoidAsync(saleId, new VoidSaleRequest(reason));
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
            return;
        }

        ServiceLocator.Resolve<PosHandoffService>().PendingCorrectionSaleId = saleId;
        ServiceLocator.Resolve<NavigationService>().RequestMenuNavigation("pos");
        _toast.Success(L["sale_voided"]);
        RequestClose?.Invoke(this, ReceiptDialogResult.Corrected);
    }

    [RelayCommand]
    private void ReturnSale()
    {
        if (Receipt is null || !CanReturnSale) return;
        ServiceLocator.Resolve<ReturnsViewModel>().StartForSale(Receipt.SaleId);
        ServiceLocator.Resolve<NavigationService>().RequestMenuNavigation("returns");
        RequestClose?.Invoke(this, ReceiptDialogResult.Returned);
    }

    [RelayCommand]
    private void NewSale() => RequestClose?.Invoke(this, ReceiptDialogResult.NewSale);

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private void CloseModal() => RequestClose?.Invoke(this, ReceiptDialogResult.Closed);

    public void Close() => RequestClose?.Invoke(this, ReceiptDialogResult.Closed);
}
