using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Settings;
using Cartex.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.UI.ViewModels;

public partial class SalesPolicyViewModel : ViewModelBase, ILoadable
{
    private readonly ISettingsApi _settingsApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly AuthService _auth;

    public SalesPolicyViewModel(ISettingsApi settingsApi, IToastService toast, IBusyService busy, AuthService auth)
    {
        _settingsApi = settingsApi;
        _toast = toast;
        _busy = busy;
        _auth = auth;
        // A ComboBox bound to an empty ItemsSource coerces SelectedIndex to -1 and writes it
        // back, so the options must exist before the view binds — not once loading finishes.
        FillOptions(ShiftPolicies, ShiftPolicyCodes, code => L[$"shift_policy_{code.ToLowerInvariant()}"]);
        FillOptions(CorrectionWindows, CorrectionWindowCodes, code => L[$"correction_{code.ToLowerInvariant()}"]);
        FillOptions(CustomerRequirements, CustomerRequirementCodes, code => L[$"customer_req_{code.ToLowerInvariant()}"]);
    }

    private static readonly string[] ShiftPolicyCodes = ["Off", "CashOnly", "AllSales"];
    private static readonly string[] CorrectionWindowCodes = ["Off", "Shift", "BusinessDay", "Days", "Always"];
    private static readonly string[] CustomerRequirementCodes = ["Optional", "OnDebt", "Always"];

    public ObservableCollection<string> ShiftPolicies { get; } = [];
    public ObservableCollection<string> CorrectionWindows { get; } = [];
    public ObservableCollection<string> CustomerRequirements { get; } = [];

    [ObservableProperty] private int _shiftPolicyIndex = 1;
    [ObservableProperty] private int _correctionWindowIndex = 1;
    [ObservableProperty] private decimal _saleCorrectionDays = 1;
    [ObservableProperty] private decimal _maxDiscountPercent;
    [ObservableProperty] private decimal _maxDebtWriteOffAmount;
    [ObservableProperty] private decimal _maxDebtWriteOffPercent;
    [ObservableProperty] private decimal _maxPriceIncreasePercent;
    [ObservableProperty] private decimal _defaultMinStock;
    [ObservableProperty] private decimal _staleRateDays = 3;
    [ObservableProperty] private bool _allowDebtSales = true;
    [ObservableProperty] private bool _allowCustomerCredit;
    [ObservableProperty] private bool _requireDebtDueDate = true;
    [ObservableProperty] private bool _requireSupplier;
    [ObservableProperty] private bool _showOutOfStock;
    [ObservableProperty] private bool _showUnlistedProducts = true;
    [ObservableProperty] private bool _allowInsufficientStockSales;
    [ObservableProperty] private bool _allowRetroactiveCashback;
    [ObservableProperty] private bool _updateCatalogPriceOnSale = true;
    [ObservableProperty] private bool _allowDebtWriteOff = true;
    [ObservableProperty] private bool _printMoneyDocuments = true;
    [ObservableProperty] private bool _allowConsolidatedAct = true;
    [ObservableProperty] private bool _allowCustomerLoans;
    [ObservableProperty] private decimal _maxCustomerLoan;
    [ObservableProperty] private int _customerRequirementIndex = 1;
    [ObservableProperty] private bool _allowReturnOnVoidedSale;
    [ObservableProperty] private bool _allowFreeReturnLines = true;
    [ObservableProperty] private bool _requireReturnReason;
    [ObservableProperty] private bool _allowSaleQueue = true;

    public bool ShowCorrectionDays => CorrectionWindowIndex == 3;
    public bool CanEdit => _auth.HasPermission("settings.salesPolicy");

    partial void OnCorrectionWindowIndexChanged(int value) => OnPropertyChanged(nameof(ShowCorrectionDays));

    private SalesPolicyDto _loaded = new();

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
                _loaded = await _settingsApi.GetSalesPolicyAsync();

            ShiftPolicyIndex = Math.Max(0, Array.IndexOf(ShiftPolicyCodes, _loaded.ShiftPolicy));
            CorrectionWindowIndex = Math.Max(0, Array.IndexOf(CorrectionWindowCodes, _loaded.SaleCorrectionWindow));
            SaleCorrectionDays = _loaded.SaleCorrectionDays;
            MaxDiscountPercent = _loaded.MaxDiscountPercent;
            MaxDebtWriteOffAmount = _loaded.MaxDebtWriteOffAmount;
            MaxDebtWriteOffPercent = _loaded.MaxDebtWriteOffPercent;
            MaxPriceIncreasePercent = _loaded.MaxPriceIncreasePercent;
            DefaultMinStock = _loaded.DefaultMinStock;
            StaleRateDays = _loaded.StaleRateDays;
            AllowDebtSales = _loaded.AllowDebtSales;
            AllowCustomerCredit = _loaded.AllowCustomerCredit;
            RequireDebtDueDate = _loaded.RequireDebtDueDate;
            RequireSupplier = _loaded.RequireSupplier;
            ShowOutOfStock = _loaded.ShowOutOfStock;
            ShowUnlistedProducts = _loaded.ShowUnlistedProducts;
            AllowInsufficientStockSales = _loaded.AllowInsufficientStockSales;
            AllowRetroactiveCashback = _loaded.AllowRetroactiveCashback;
            UpdateCatalogPriceOnSale = _loaded.UpdateCatalogPriceOnSale;
            AllowDebtWriteOff = _loaded.AllowDebtWriteOff;
            PrintMoneyDocuments = _loaded.PrintMoneyDocuments;
            AllowConsolidatedAct = _loaded.AllowConsolidatedAct;
            AllowCustomerLoans = _loaded.AllowCustomerLoans;
            MaxCustomerLoan = _loaded.MaxCustomerLoan;
            CustomerRequirementIndex = Math.Max(0, Array.IndexOf(CustomerRequirementCodes, _loaded.CustomerRequirement));
            AllowReturnOnVoidedSale = _loaded.AllowReturnOnVoidedSale;
            AllowFreeReturnLines = _loaded.AllowFreeReturnLines;
            RequireReturnReason = _loaded.RequireReturnReason;
            AllowSaleQueue = _loaded.AllowSaleQueue;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanEdit) return;
        try
        {
            // Sent as a whole document off the record that was loaded, so a field this screen does
            // not show keeps its stored value instead of quietly resetting to the type default.
            var policy = _loaded with
            {
                ShiftPolicy = CodeAt(ShiftPolicyCodes, ShiftPolicyIndex, _loaded.ShiftPolicy),
                SaleCorrectionWindow = CodeAt(CorrectionWindowCodes, CorrectionWindowIndex, _loaded.SaleCorrectionWindow),
                SaleCorrectionDays = (int)Math.Clamp(SaleCorrectionDays, 1, 365),
                MaxDiscountPercent = MaxDiscountPercent,
                MaxDebtWriteOffAmount = MaxDebtWriteOffAmount,
                MaxDebtWriteOffPercent = MaxDebtWriteOffPercent,
                MaxPriceIncreasePercent = MaxPriceIncreasePercent,
                DefaultMinStock = DefaultMinStock,
                StaleRateDays = (int)Math.Clamp(StaleRateDays, 1, 30),
                AllowDebtSales = AllowDebtSales,
                AllowCustomerCredit = AllowCustomerCredit,
                RequireDebtDueDate = RequireDebtDueDate,
                RequireSupplier = RequireSupplier,
                ShowOutOfStock = ShowOutOfStock,
                ShowUnlistedProducts = ShowUnlistedProducts,
                AllowInsufficientStockSales = AllowInsufficientStockSales,
                AllowRetroactiveCashback = AllowRetroactiveCashback,
                UpdateCatalogPriceOnSale = UpdateCatalogPriceOnSale,
                AllowDebtWriteOff = AllowDebtWriteOff,
                PrintMoneyDocuments = PrintMoneyDocuments,
                AllowConsolidatedAct = AllowConsolidatedAct,
                AllowCustomerLoans = AllowCustomerLoans,
                MaxCustomerLoan = MaxCustomerLoan,
                CustomerRequirement = CodeAt(CustomerRequirementCodes, CustomerRequirementIndex, _loaded.CustomerRequirement),
                AllowReturnOnVoidedSale = AllowReturnOnVoidedSale,
                AllowFreeReturnLines = AllowFreeReturnLines,
                RequireReturnReason = RequireReturnReason,
                AllowSaleQueue = AllowSaleQueue
            };

            using (_busy.Begin(L["loading"]))
                await _settingsApi.UpdateSalesPolicyAsync(policy);

            _loaded = policy;
            ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.SalesPolicy);
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private static string CodeAt(string[] codes, int index, string fallback) =>
        index >= 0 && index < codes.Length ? codes[index] : fallback;

    private static void FillOptions(ObservableCollection<string> target, string[] codes, Func<string, string> label)
    {
        target.Clear();
        foreach (var code in codes) target.Add(label(code));
    }
}
