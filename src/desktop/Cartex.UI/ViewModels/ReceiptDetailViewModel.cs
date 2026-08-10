using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
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
        IsPosCheckoutMode = isPosCheckoutMode;

        if (preloadedReceipt is not null)
            ApplyReceipt(preloadedReceipt);
    }

    [ObservableProperty] private ReceiptDto? _receipt;
    [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? _receiptQrCode;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isPosCheckoutMode;

    public bool HasReceipt => Receipt is not null;
    public bool CanAttachCustomer => _auth.HasPermission("sales.edit") && Receipt is not null && string.IsNullOrEmpty(Receipt.CustomerName);
    public bool CanAttachCase => _auth.HasPermission("trade_cases.edit") && Receipt is not null && !IsPosCheckoutMode;
    public bool CanReturnSale => _auth.HasPermission("sales.return") && !IsPosCheckoutMode && Receipt is not null;
    public bool CanPrint => _auth.HasPermission("printing.receipts.reprint") && Receipt is not null;

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

    [RelayCommand]
    private async Task AttachCustomerAsync()
    {
        if (Receipt is null || !CanAttachCustomer) return;
        var vm = ServiceLocator.Resolve<CustomerPickerViewModel>();
        await vm.InitAsync();
        var customer = await _dialog.ShowAsync<CustomerPickerDialog, CustomerPickerViewModel, CustomerDto>(vm);
        if (customer is null) return;

        try
        {
            if (_saleId is not null && _saleId.Value > 0)
            {
                using (_busy.Begin(L["loading"]))
                    await _salesApi.AssignCustomerAsync(_saleId.Value, customer.Id);
                _toast.Success(L["customer_attached_to_sale"]);
                _customerId = customer.Id;
                Receipt = Receipt with { CustomerName = $"{customer.FullName} {customer.LastName}".Trim() };
                OnPropertyChanged(nameof(CanAttachCustomer));
                RequestClose?.Invoke(this, ReceiptDialogResult.CustomerAssigned);
            }
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    [RelayCommand]
    private async Task AttachCaseAsync()
    {
        if (Receipt is null || !CanAttachCase) return;
        if (_saleId is not { } saleId || saleId == 0) return;

        if (string.IsNullOrEmpty(Receipt.CustomerName) && _customerId is null)
        {
            _toast.Warning(L["case_customer_required"] ?? "Loyihaga biriktirish uchun mijoz tanlangan bo'lishi kerak.");
            return;
        }

        try
        {
            long custId = _customerId ?? 0;
            string custName = Receipt.CustomerName ?? "";
            var vm = new SaleCaseAttachViewModel(
                ServiceLocator.Resolve<ITradeCasesApi>(),
                _toast,
                _busy,
                ServiceLocator.Resolve<BranchContextService>());
            await vm.InitAsync(custId, custName);

            var result = await _dialog.ShowAsync<SaleCaseAttachDialog, SaleCaseAttachViewModel, object>(vm);
            var (caseId, caseNumber) = result switch
            {
                TradeCaseCreatedDto created => (created.Id, created.CaseNumber),
                TradeCaseListDto existing => (existing.Id, existing.CaseNumber),
                _ => (0L, null)
            };

            if (caseNumber is not null && caseId > 0)
            {
                using (_busy.Begin(L["loading"]))
                    await ServiceLocator.Resolve<ITradeCasesApi>().LinkSaleAsync(caseId, saleId);
                _toast.Success(string.Format(L["case_linked_fmt"], caseNumber));
            }
        }
        catch (Exception ex)
        {
            _toast.Error(ApiErrors.Describe(ex));
        }
    }

    [RelayCommand]
    private async Task ReturnSaleAsync()
    {
        if (Receipt is null || !CanReturnSale) return;
        long sid = _saleId ?? 0;
        var vm = new ReturnSaleViewModel(sid, _salesApi, _toast, _busy);
        await vm.InitAsync();
        var returned = await _dialog.ShowAsync<ReturnSaleDialog, ReturnSaleViewModel, bool>(vm);
        if (returned)
        {
            RequestClose?.Invoke(this, ReceiptDialogResult.Returned);
        }
    }

    public event EventHandler<object?>? RequestClose;

    [RelayCommand]
    private void CloseModal() => Close();

    public void Close() => RequestClose?.Invoke(this, ReceiptDialogResult.Closed);

    [RelayCommand]
    private void NewSale() => RequestClose?.Invoke(this, ReceiptDialogResult.NewSale);
}
