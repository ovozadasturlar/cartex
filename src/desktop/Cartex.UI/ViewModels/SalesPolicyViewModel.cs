using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Features;
using Cartex.Shared.Models.Settings;
using Cartex.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.UI.ViewModels;

public partial class SalesPolicyViewModel : ViewModelBase, ILoadable
{
    private readonly ISettingsApi _settingsApi;
    private readonly IFeaturesApi _featuresApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly AuthService _auth;
    private readonly PrintPolicyCache _printPolicyCache;

    public SalesPolicyViewModel(
        ISettingsApi settingsApi,
        IFeaturesApi featuresApi,
        IToastService toast,
        IBusyService busy,
        AuthService auth,
        PrintPolicyCache printPolicyCache)
    {
        _settingsApi = settingsApi;
        _featuresApi = featuresApi;
        _toast = toast;
        _busy = busy;
        _auth = auth;
        _printPolicyCache = printPolicyCache;
        // A ComboBox bound to an empty ItemsSource coerces SelectedIndex to -1 and writes it
        // back, so the options must exist before the view binds — not once loading finishes.
        FillOptions(ShiftPolicies, ShiftPolicyCodes, code => L[$"shift_policy_{code.ToLowerInvariant()}"]);
        FillOptions(CorrectionWindows, CorrectionWindowCodes, code => L[$"correction_{code.ToLowerInvariant()}"]);
        FillOptions(CustomerRequirements, CustomerRequirementCodes, code => L[$"customer_req_{code.ToLowerInvariant()}"]);
        FillOptions(CreditLimitEnforcements, CreditLimitEnforcementCodes, code => L[$"credit_limit_{code.ToLowerInvariant()}"]);
    }

    private static readonly string[] ShiftPolicyCodes = ["Off", "CashOnly", "AllSales"];
    private static readonly string[] CorrectionWindowCodes = ["Off", "Shift", "BusinessDay", "Days", "Always"];
    // "Optional" olib tashlandi: u "OnDebt" bilan bir xil ishlardi. "OnBonus" esa haqiqiy
    // uchinchi holat — do'kon cashback bersa, mijozsiz savdo bonusni yo'qotadi.
    private static readonly string[] CustomerRequirementCodes = ["OnDebt", "OnBonus", "Always"];
    // QARZ-22: limitdan oshishni rad etish yoki ogohlantirib o'tkazish.
    private static readonly string[] CreditLimitEnforcementCodes = ["Block", "Warn"];

    public ObservableCollection<string> ShiftPolicies { get; } = [];
    public ObservableCollection<string> CorrectionWindows { get; } = [];
    public ObservableCollection<string> CustomerRequirements { get; } = [];
    public ObservableCollection<string> CreditLimitEnforcements { get; } = [];

    [ObservableProperty] private int _shiftPolicyIndex = 1;
    [ObservableProperty] private int _correctionWindowIndex = 1;
    [ObservableProperty] private decimal _saleCorrectionDays = 1;
    [ObservableProperty] private decimal? _maxDiscountPercent;
    [ObservableProperty] private decimal? _maxDebtWriteOffAmount;
    [ObservableProperty] private decimal? _maxDebtWriteOffPercent;
    [ObservableProperty] private decimal? _maxPriceIncreasePercent;
    [ObservableProperty] private int _priceDriftWindowMinutes = 60;
    [ObservableProperty] private decimal _defaultMinStock;
    [ObservableProperty] private decimal _staleRateDays = 3;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowDebtSalesHint))]
    private bool _allowDebtSales = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowCustomerCreditHint))]
    private bool _allowCustomerCredit;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequireDebtDueDateHint))]
    private bool _requireDebtDueDate = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequireSupplierHint))]
    private bool _requireSupplier;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowOutOfStockHint))]
    private bool _showOutOfStock;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowUnlistedProductsHint))]
    private bool _showUnlistedProducts = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowInsufficientStockSalesHint))]
    private bool _allowInsufficientStockSales;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowNegativeStockOfflineHint))]
    private bool _allowNegativeStockWhenOffline;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowRetroactiveCashbackHint))]
    private bool _allowRetroactiveCashback;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateCatalogPriceOnSaleHint))]
    private bool _updateCatalogPriceOnSale = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowDebtWriteOffHint))]
    private bool _allowDebtWriteOff = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrintMoneyDocumentsHint))]
    private bool _printMoneyDocuments = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrintCartProformaHint))]
    private bool _printCartProforma = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowConsolidatedActHint))]
    private bool _allowConsolidatedAct = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowCustomerLoansHint))]
    private bool _allowCustomerLoans;
    [ObservableProperty] private decimal? _maxCustomerLoan;
    [ObservableProperty] private decimal? _defaultCreditLimit;
    [ObservableProperty] private int _customerRequirementIndex = 1;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CreditLimitEnforcementHint))]
    private int _creditLimitEnforcementIndex;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowReturnOnVoidedSaleHint))]
    private bool _allowReturnOnVoidedSale;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowFreeReturnLinesHint))]
    private bool _allowFreeReturnLines = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequireReturnReasonHint))]
    private bool _requireReturnReason;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllowSaleQueueHint))]
    private bool _allowSaleQueue = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TrackWriteOffHint))]
    private bool _trackWriteOff;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTogglePricingMulticurrency))]
    [NotifyPropertyChangedFor(nameof(CanToggleSalesMulticurrency))]
    private bool _multicurrencyLicensed;
    [ObservableProperty] private bool _pricingMulticurrencyEnabled;
    [ObservableProperty] private bool _salesMulticurrencyEnabled;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTogglePricingMulticurrency))]
    private OwnerModuleDto? _pricingMulticurrencyModule;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggleSalesMulticurrency))]
    private OwnerModuleDto? _salesMulticurrencyModule;

    /// Har kalitning ostida uning hozirgi holati nimani anglatishi yozilib turadi — egasi
    /// tugmani bosmasdan oldin oqibatini o'qiy oladi.
    private string Hint(bool on, string key) => L[$"{key}_{(on ? "on" : "off")}"];

    public string AllowDebtWriteOffHint => Hint(AllowDebtWriteOff, "allow_debt_write_off");
    public string PrintMoneyDocumentsHint => Hint(PrintMoneyDocuments, "print_money_documents");
    public string PrintCartProformaHint => Hint(PrintCartProforma, "print_cart_proforma");
    public string AllowConsolidatedActHint => Hint(AllowConsolidatedAct, "allow_consolidated_act");
    public string AllowSaleQueueHint => Hint(AllowSaleQueue, "allow_sale_queue");
    public string UpdateCatalogPriceOnSaleHint => Hint(UpdateCatalogPriceOnSale, "update_catalog_price_on_sale");
    public string AllowDebtSalesHint => Hint(AllowDebtSales, "allow_debt_sales");
    public string CreditLimitEnforcementHint =>
        L[$"credit_limit_{CodeAt(CreditLimitEnforcementCodes, CreditLimitEnforcementIndex, "Block").ToLowerInvariant()}_hint"];
    public string RequireDebtDueDateHint => Hint(RequireDebtDueDate, "require_debt_due_date");
    public string AllowCustomerCreditHint => Hint(AllowCustomerCredit, "allow_customer_credit");
    public string AllowRetroactiveCashbackHint => Hint(AllowRetroactiveCashback, "allow_retroactive_cashback");
    public string AllowCustomerLoansHint => Hint(AllowCustomerLoans, "allow_customer_loans");
    public string AllowReturnOnVoidedSaleHint => Hint(AllowReturnOnVoidedSale, "allow_return_on_voided_sale");
    public string AllowFreeReturnLinesHint => Hint(AllowFreeReturnLines, "allow_free_return_lines");
    public string RequireReturnReasonHint => Hint(RequireReturnReason, "require_return_reason");
    public string RequireSupplierHint => Hint(RequireSupplier, "require_supplier");
    public string ShowOutOfStockHint => Hint(ShowOutOfStock, "show_out_of_stock");
    public string ShowUnlistedProductsHint => Hint(ShowUnlistedProducts, "show_unlisted_products");
    public string AllowInsufficientStockSalesHint => Hint(AllowInsufficientStockSales, "allow_insufficient_stock_sales");
    public string AllowNegativeStockOfflineHint => Hint(AllowNegativeStockWhenOffline, "allow_negative_stock_offline");
    public string TrackWriteOffHint => Hint(TrackWriteOff, "track_write_off");

    public bool ShowCorrectionDays => CorrectionWindowIndex == 3;
    public bool CanEdit => _auth.HasPermission("settings.salesPolicy");
    public bool CanTogglePricingMulticurrency =>
        CanEdit && _auth.HasPermission("business.edit") && MulticurrencyLicensed && PricingMulticurrencyModule?.Available == true;
    public bool CanToggleSalesMulticurrency =>
        CanEdit && _auth.HasPermission("business.edit") && MulticurrencyLicensed && SalesMulticurrencyModule?.Available == true;

    partial void OnCorrectionWindowIndexChanged(int value) => OnPropertyChanged(nameof(ShowCorrectionDays));

    private SalesPolicyDto _loaded = new();

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var policyTask = _settingsApi.GetSalesPolicyAsync();
                var enabledTask = _featuresApi.GetEnabledAsync();
                var modulesTask = _auth.HasPermission("business.edit")
                    ? _featuresApi.GetModulesAsync()
                    : Task.FromResult(new List<OwnerModuleDto>());
                await Task.WhenAll(policyTask, enabledTask, modulesTask);

                _loaded = await policyTask;
                ApplyCurrencyFeatures(await enabledTask, await modulesTask);
            }

            ShiftPolicyIndex = Math.Max(0, Array.IndexOf(ShiftPolicyCodes, _loaded.ShiftPolicy));
            CorrectionWindowIndex = Math.Max(0, Array.IndexOf(CorrectionWindowCodes, _loaded.SaleCorrectionWindow));
            SaleCorrectionDays = _loaded.SaleCorrectionDays;
            MaxDiscountPercent = _loaded.MaxDiscountPercent;
            MaxDebtWriteOffAmount = _loaded.MaxDebtWriteOffAmount;
            MaxDebtWriteOffPercent = _loaded.MaxDebtWriteOffPercent;
            MaxPriceIncreasePercent = _loaded.MaxPriceIncreasePercent;
            PriceDriftWindowMinutes = _loaded.PriceDriftWindowMinutes;
            DefaultMinStock = _loaded.DefaultMinStock;
            StaleRateDays = _loaded.StaleRateDays;
            AllowDebtSales = _loaded.AllowDebtSales;
            AllowCustomerCredit = _loaded.AllowCustomerCredit;
            RequireDebtDueDate = _loaded.RequireDebtDueDate;
            RequireSupplier = _loaded.RequireSupplier;
            ShowOutOfStock = _loaded.ShowOutOfStock;
            ShowUnlistedProducts = _loaded.ShowUnlistedProducts;
            AllowInsufficientStockSales = _loaded.AllowInsufficientStockSales;
            AllowNegativeStockWhenOffline = _loaded.AllowNegativeStockWhenOffline;
            AllowRetroactiveCashback = _loaded.AllowRetroactiveCashback;
            UpdateCatalogPriceOnSale = _loaded.UpdateCatalogPriceOnSale;
            AllowDebtWriteOff = _loaded.AllowDebtWriteOff;
            PrintMoneyDocuments = _loaded.PrintMoneyDocuments;
            PrintCartProforma = _loaded.PrintCartProforma;
            AllowConsolidatedAct = _loaded.AllowConsolidatedAct;
            AllowCustomerLoans = _loaded.AllowCustomerLoans;
            MaxCustomerLoan = _loaded.MaxCustomerLoan;
            DefaultCreditLimit = _loaded.DefaultCreditLimit;
            CustomerRequirementIndex = Math.Max(0, Array.IndexOf(CustomerRequirementCodes, _loaded.CustomerRequirement));
            CreditLimitEnforcementIndex = Math.Max(0, Array.IndexOf(CreditLimitEnforcementCodes, _loaded.CreditLimitEnforcement));
            AllowReturnOnVoidedSale = _loaded.AllowReturnOnVoidedSale;
            AllowFreeReturnLines = _loaded.AllowFreeReturnLines;
            RequireReturnReason = _loaded.RequireReturnReason;
            AllowSaleQueue = _loaded.AllowSaleQueue;
            TrackWriteOff = _loaded.TrackWriteOff;
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
                PriceDriftWindowMinutes = PriceDriftWindowMinutes,
                DefaultMinStock = DefaultMinStock,
                StaleRateDays = (int)Math.Clamp(StaleRateDays, 1, 30),
                AllowDebtSales = AllowDebtSales,
                AllowCustomerCredit = AllowCustomerCredit,
                RequireDebtDueDate = RequireDebtDueDate,
                RequireSupplier = RequireSupplier,
                ShowOutOfStock = ShowOutOfStock,
                ShowUnlistedProducts = ShowUnlistedProducts,
                AllowInsufficientStockSales = AllowInsufficientStockSales,
                AllowNegativeStockWhenOffline = AllowNegativeStockWhenOffline,
                AllowRetroactiveCashback = AllowRetroactiveCashback,
                UpdateCatalogPriceOnSale = UpdateCatalogPriceOnSale,
                AllowDebtWriteOff = AllowDebtWriteOff,
                PrintMoneyDocuments = PrintMoneyDocuments,
                PrintCartProforma = PrintCartProforma,
                AllowConsolidatedAct = AllowConsolidatedAct,
                AllowCustomerLoans = AllowCustomerLoans,
                MaxCustomerLoan = MaxCustomerLoan,
                DefaultCreditLimit = DefaultCreditLimit,
                CustomerRequirement = CodeAt(CustomerRequirementCodes, CustomerRequirementIndex, _loaded.CustomerRequirement),
                CreditLimitEnforcement = CodeAt(CreditLimitEnforcementCodes, CreditLimitEnforcementIndex, _loaded.CreditLimitEnforcement),
                AllowReturnOnVoidedSale = AllowReturnOnVoidedSale,
                AllowFreeReturnLines = AllowFreeReturnLines,
                RequireReturnReason = RequireReturnReason,
                AllowSaleQueue = AllowSaleQueue,
                TrackWriteOff = TrackWriteOff
            };

            using (_busy.Begin(L["loading"]))
                await _settingsApi.UpdateSalesPolicyAsync(policy);

            _loaded = policy;
            ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.SalesPolicy);
            await ServiceLocator.Resolve<NavigationService>().RequestFeaturesRefreshAsync();
            await _printPolicyCache.RefreshAsync();
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private Task TogglePricingMulticurrencyAsync() =>
        ToggleMulticurrencyAsync(PricingMulticurrencyModule, CanTogglePricingMulticurrency);

    [RelayCommand]
    private Task ToggleSalesMulticurrencyAsync() =>
        ToggleMulticurrencyAsync(SalesMulticurrencyModule, CanToggleSalesMulticurrency);

    private async Task ToggleMulticurrencyAsync(OwnerModuleDto? module, bool canToggle)
    {
        if (!canToggle || module is null) return;
        try
        {
            using (_busy.Begin(L["loading"]))
                await _featuresApi.SetModuleAsync(module.Code, new SetFeatureRequest(!module.IsEnabled));

            ServiceLocator.Resolve<ReferenceCache>().Invalidate(CacheKeys.Features);
            await ServiceLocator.Resolve<NavigationService>().RequestFeaturesRefreshAsync();
            await LoadCurrencyFeaturesAsync();
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task LoadCurrencyFeaturesAsync()
    {
        var enabledTask = _featuresApi.GetEnabledAsync();
        var modulesTask = _featuresApi.GetModulesAsync();
        await Task.WhenAll(enabledTask, modulesTask);
        ApplyCurrencyFeatures(await enabledTask, await modulesTask);
    }

    private void ApplyCurrencyFeatures(IReadOnlyCollection<string> enabled, IEnumerable<OwnerModuleDto> modules)
    {
        PricingMulticurrencyModule = modules.FirstOrDefault(x => x.Code == "multicurrency_pricing");
        SalesMulticurrencyModule = modules.FirstOrDefault(x => x.Code == "multicurrency_sales");
        // multicurrency (asosiy) litsenziyasi bolasining `Available` flagidan — faol holatidan emas (SOZ-08b).
        MulticurrencyLicensed = PricingMulticurrencyModule?.Available == true;
        PricingMulticurrencyEnabled = enabled.Contains("multicurrency_pricing", StringComparer.Ordinal);
        SalesMulticurrencyEnabled = enabled.Contains("multicurrency_sales", StringComparer.Ordinal);
    }

    private static string CodeAt(string[] codes, int index, string fallback) =>
        index >= 0 && index < codes.Length ? codes[index] : fallback;

    private static void FillOptions(ObservableCollection<string> target, string[] codes, Func<string, string> label)
    {
        target.Clear();
        foreach (var code in codes) target.Add(label(code));
    }
}
