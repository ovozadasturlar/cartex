using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.Shared.Models.Categories;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Loyalty;
using Cartex.Shared.Models.Products;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public sealed record DiscountRow(DiscountRuleDto Dto, string ScopeText, string ValueText, string ConditionText, string PeriodText, string StatusText, bool IsEnabled);

public sealed record ExceptionChip(string Scope, long TargetId, string Display);

public partial class LoyaltyViewModel : ViewModelBase, ILoadable
{
    private readonly ILoyaltyApi _api;
    private readonly IProductsApi _productsApi;
    private readonly ICategoriesApi _categoriesApi;
    private readonly ICustomersApi _customersApi;
    private readonly IManufacturersApi _manufacturersApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly AuthService _auth;
    private readonly ReferenceCache _cache;
    private long _editRuleId;
    private long _editDiscountId;

    public bool CanManage => _auth.HasPermission("loyalty.edit");

    [ObservableProperty] private string _sectionKey = "discounts";
    public bool IsDiscountSection => SectionKey == "discounts";
    public bool IsBonusSection => SectionKey == "bonus";
    public bool IsSettingsSection => SectionKey == "settings";

    partial void OnSectionKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsDiscountSection));
        OnPropertyChanged(nameof(IsBonusSection));
        OnPropertyChanged(nameof(IsSettingsSection));
    }

    [RelayCommand]
    private void SelectSection(string key) => SectionKey = key;

    [ObservableProperty] private int _statPeriodIndex = 1;
    [ObservableProperty] private string _statDiscountTotal = "0";
    [ObservableProperty] private string _statDiscountSales = "0";
    [ObservableProperty] private string _statDiscountShare = "0%";
    [ObservableProperty] private string _statBonusOutstanding = "0";
    public bool IsPeriodDay => StatPeriodIndex == 0;
    public bool IsPeriodWeek => StatPeriodIndex == 1;
    public bool IsPeriodMonth => StatPeriodIndex == 2;

    [RelayCommand]
    private void SetStatPeriod(string index) => StatPeriodIndex = int.Parse(index);

    partial void OnStatPeriodIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsPeriodDay));
        OnPropertyChanged(nameof(IsPeriodWeek));
        OnPropertyChanged(nameof(IsPeriodMonth));
        _ = LoadStatsAsync();
    }

    private async Task LoadStatsAsync()
    {
        try
        {
            var to = DateTime.UtcNow.AddDays(1);
            var from = StatPeriodIndex switch
            {
                0 => DateTime.UtcNow.Date,
                1 => DateTime.UtcNow.Date.AddDays(-7),
                _ => DateTime.UtcNow.Date.AddDays(-30)
            };
            var stats = await _api.GetStatsAsync(from, to);
            StatDiscountTotal = stats.DiscountTotal.ToString("N0");
            StatDiscountSales = $"{stats.DiscountedSales} / {stats.SalesCount}";
            StatDiscountShare = stats.GrossTotal > 0 ? $"{stats.DiscountTotal / stats.GrossTotal * 100:0.#}%" : "0%";
            StatBonusOutstanding = stats.BonusOutstanding.ToString("N0");
        }
        catch { }
    }

    public ObservableCollection<LabeledValue> Scopes { get; } = [];
    public ObservableCollection<LabeledValue> Methods { get; } = [];
    public ObservableCollection<CashbackRuleDto> Rules { get; } = [];
    public ObservableCollection<ProductOptionDto> Products { get; } = [];
    public ObservableCollection<CategoryDto> Categories { get; } = [];
    public ObservableCollection<CustomerDto> CustomerResults { get; } = [];
    public ObservableCollection<ManufacturerDto> Manufacturers { get; } = [];
    public ObservableCollection<DiscountRow> DiscountRules { get; } = [];
    public ObservableCollection<LabeledValue> DiscountScopes { get; } = [];
    public ObservableCollection<LabeledValue> DiscountMethods { get; } = [];
    public ObservableCollection<LabeledValue> ExceptionScopes { get; } = [];
    public ObservableCollection<LabeledValue> CombineModes { get; } = [];
    public ObservableCollection<ExceptionChip> DiscountExceptions { get; } = [];

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private decimal _totalPercent;
    [ObservableProperty] private int _roundingIndex;
    [ObservableProperty] private int _combineModeIndex;
    public string[] RoundingOptions { get; } = ["0", "1", "100", "1000"];
    private static readonly decimal[] RoundingValues = [0m, 1m, 100m, 1000m];

    [ObservableProperty] private bool _isRuleOpen;
    [ObservableProperty] private bool _isRuleNew;
    [ObservableProperty] private LabeledValue? _ruleScope;
    [ObservableProperty] private LabeledValue? _ruleMethod;
    [ObservableProperty] private ProductOptionDto? _ruleProduct;
    [ObservableProperty] private CategoryDto? _ruleCategory;
    [ObservableProperty] private decimal _ruleValue;
    [ObservableProperty] private int _rulePriority;
    [ObservableProperty] private bool _ruleExcludeFromTotal;

    [ObservableProperty] private bool _isDiscountOpen;
    [ObservableProperty] private bool _isDiscountNew;
    [ObservableProperty] private string _discountName = "";
    [ObservableProperty] private bool _discountEnabled = true;
    [ObservableProperty] private LabeledValue? _discountScope;
    [ObservableProperty] private LabeledValue? _discountMethod;
    [ObservableProperty] private ProductOptionDto? _discountProduct;
    [ObservableProperty] private CategoryDto? _discountCategory;
    [ObservableProperty] private ManufacturerDto? _discountManufacturer;
    [ObservableProperty] private CustomerDto? _discountCustomer;
    private static readonly CustomerDto EmptyDiscountCustomer = new(0, "", null, null, null, null, null, 0, 0, 0, 0);
    public CustomerDto DiscountCustomerDisplay => DiscountCustomer ?? EmptyDiscountCustomer;

    partial void OnDiscountCustomerChanged(CustomerDto? value) => OnPropertyChanged(nameof(DiscountCustomerDisplay));
    [ObservableProperty] private string _customerSearch = "";
    private CancellationTokenSource? _customerSearchCts;

    partial void OnCustomerSearchChanged(string value)
    {
        var cts = Debounce.Restart(ref _customerSearchCts);
        _ = DebouncedCustomerSearchAsync(cts.Token);
    }

    private async Task DebouncedCustomerSearchAsync(CancellationToken token)
    {
        try { await Task.Delay(300, token); } catch { return; }
        if (token.IsCancellationRequested) return;
        var query = CustomerSearch.Trim();
        CustomerResults.Clear();
        if (query.Length < 2) return;
        try
        {
            var result = await _customersApi.QueryAsync(QueryRequest.Create().Page(1, 20).Search(query).Build());
            if (token.IsCancellationRequested) return;
            foreach (var c in result.Content ?? []) CustomerResults.Add(c);
        }
        catch { }
    }

    [RelayCommand]
    private void PickDiscountCustomer(CustomerDto customer)
    {
        DiscountCustomer = customer;
        CustomerSearch = "";
        CustomerResults.Clear();
    }

    [ObservableProperty] private decimal _discountMinAmount;
    [ObservableProperty] private decimal _discountValue;
    [ObservableProperty] private int _discountPriority;
    [ObservableProperty] private DateTime? _discountStartsOn;
    [ObservableProperty] private DateTime? _discountEndsOn;

    public bool IsModalOpen => IsRuleOpen || IsDiscountOpen;

    partial void OnIsRuleOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    partial void OnIsDiscountOpenChanged(bool value) => OnPropertyChanged(nameof(IsModalOpen));
    [ObservableProperty] private LabeledValue? _exceptionScope;
    [ObservableProperty] private ProductOptionDto? _exceptionProduct;
    [ObservableProperty] private CategoryDto? _exceptionCategory;
    [ObservableProperty] private ManufacturerDto? _exceptionManufacturer;

    public bool IsExceptionProduct => ExceptionScope?.Value != "Category" && ExceptionScope?.Value != "Manufacturer";
    public bool IsExceptionCategory => ExceptionScope?.Value == "Category";
    public bool IsExceptionManufacturer => ExceptionScope?.Value == "Manufacturer";

    partial void OnExceptionScopeChanged(LabeledValue? value)
    {
        OnPropertyChanged(nameof(IsExceptionProduct));
        OnPropertyChanged(nameof(IsExceptionCategory));
        OnPropertyChanged(nameof(IsExceptionManufacturer));
    }

    public bool IsProductScope => RuleScope?.Value == "Product";
    public bool RulesEmpty => Rules.Count == 0;
    public bool DiscountsEmpty => DiscountRules.Count == 0;
    public bool IsDiscountProductScope => DiscountScope?.Value == "Product";
    public bool IsDiscountCategoryScope => DiscountScope?.Value == "Category";
    public bool IsDiscountManufacturerScope => DiscountScope?.Value == "Manufacturer";

    public LoyaltyViewModel(ILoyaltyApi api, IProductsApi productsApi, ICategoriesApi categoriesApi,
        ICustomersApi customersApi, IManufacturersApi manufacturersApi, IToastService toast, IBusyService busy, AuthService auth, ReferenceCache cache)
    {
        _cache = cache;
        _api = api;
        _productsApi = productsApi;
        _categoriesApi = categoriesApi;
        _customersApi = customersApi;
        _manufacturersApi = manufacturersApi;
        _toast = toast;
        _busy = busy;
        _auth = auth;
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                BuildLists();

                var productsTask = _cache.GetAsync(CacheKeys.ProductLookup, _productsApi.GetLookupAsync);
                var categoriesTask = _cache.GetAsync(CacheKeys.Categories, () => _categoriesApi.GetAllAsync());
                var manufacturersTask = _cache.GetAsync(CacheKeys.Manufacturers, () => _manufacturersApi.GetAllAsync());

                var products = await productsTask;
                Products.Clear();
                foreach (var p in products) Products.Add(p);

                var categories = await categoriesTask;
                Categories.Clear();
                foreach (var c in categories) Categories.Add(c);

                var manufacturers = await manufacturersTask;
                Manufacturers.Clear();
                foreach (var m in manufacturers) Manufacturers.Add(m);

                await Task.WhenAll(ReloadProgramAsync(), ReloadDiscountsAsync(), LoadStatsAsync());
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private bool _suppressProgramSave;

    private async Task ReloadProgramAsync()
    {
        var p = await _api.GetAsync();
        _suppressProgramSave = true;
        IsEnabled = p.IsEnabled;
        RoundingIndex = Math.Max(0, Array.IndexOf(RoundingValues, p.CashbackRounding));
        TotalPercent = p.TotalPercent;
        CombineModeIndex = p.DiscountCombineMode == "Stack" ? 1 : 0;
        _suppressProgramSave = false;
        Rules.Clear();
        foreach (var r in p.Rules) Rules.Add(r);
        OnPropertyChanged(nameof(RulesEmpty));
    }

    partial void OnCombineModeIndexChanged(int value)
    {
        if (!_suppressProgramSave)
            _ = SaveAsync();
    }

    private async Task ReloadDiscountsAsync()
    {
        var rules = await _api.GetDiscountRulesAsync();
        DiscountRules.Clear();
        foreach (var r in rules)
            DiscountRules.Add(ToRow(r));
        OnPropertyChanged(nameof(DiscountsEmpty));
    }

    private DiscountRow ToRow(DiscountRuleDto r)
    {
        var scope = r.Scope switch
        {
            "All" => L["discount_scope_all"],
            "Product" => $"{L["product"]}: {r.TargetName}",
            "Category" => $"{L["category"]}: {r.TargetName}",
            _ => $"{L["manufacturer"]}: {r.TargetName}"
        };
        if (r.CustomerName is not null)
            scope += $" • {r.CustomerName}";
        var value = r.Method == "Percent" ? $"{r.Value:0.##}%" : $"{r.Value:N0}";
        var condition = r.MinAmount > 0 ? $"≥ {r.MinAmount:N0}" : "—";
        var period = r.StartsOn is null && r.EndsOn is null
            ? "—"
            : $"{r.StartsOn:dd.MM.yyyy} – {r.EndsOn:dd.MM.yyyy}";
        var today = DateOnly.FromDateTime(DateTime.Now);
        var status = !r.IsEnabled ? L["discount_status_off"]
            : r.StartsOn is { } from && today < from ? L["discount_status_pending"]
            : r.EndsOn is { } to && today > to ? L["discount_status_expired"]
            : L["discount_status_active"];
        return new DiscountRow(r, scope, value, condition, period, status, r.IsEnabled);
    }

    private void BuildLists()
    {
        if (Scopes.Count > 0) return;
        Scopes.Add(new LabeledValue("Product", L["product"]));
        Scopes.Add(new LabeledValue("Category", L["category"]));
        Methods.Add(new LabeledValue("Percent", L["cashback_method_percent"]));
        Methods.Add(new LabeledValue("FixedPerUnit", L["cashback_method_fixed"]));
        DiscountScopes.Add(new LabeledValue("All", L["discount_scope_all"]));
        DiscountScopes.Add(new LabeledValue("Product", L["product"]));
        DiscountScopes.Add(new LabeledValue("Category", L["category"]));
        DiscountScopes.Add(new LabeledValue("Manufacturer", L["manufacturer"]));
        DiscountMethods.Add(new LabeledValue("Percent", L["cashback_method_percent"]));
        DiscountMethods.Add(new LabeledValue("FixedAmount", L["discount_method_amount"]));
        ExceptionScopes.Add(new LabeledValue("Product", L["product"]));
        ExceptionScopes.Add(new LabeledValue("Category", L["category"]));
        ExceptionScopes.Add(new LabeledValue("Manufacturer", L["manufacturer"]));
        CombineModes.Add(new LabeledValue("Priority", L["discount_combine_priority"]));
        CombineModes.Add(new LabeledValue("Stack", L["discount_combine_stack"]));
    }

    partial void OnRuleScopeChanged(LabeledValue? value) => OnPropertyChanged(nameof(IsProductScope));

    partial void OnDiscountScopeChanged(LabeledValue? value)
    {
        OnPropertyChanged(nameof(IsDiscountProductScope));
        OnPropertyChanged(nameof(IsDiscountCategoryScope));
        OnPropertyChanged(nameof(IsDiscountManufacturerScope));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.UpdateAsync(new UpdateLoyaltyProgramRequest(IsEnabled, TotalPercent,
                    RoundingValues[Math.Clamp(RoundingIndex, 0, 3)], CombineModeIndex == 1 ? "Stack" : "Priority"));
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void OpenCreateRule()
    {
        IsRuleNew = true;
        _editRuleId = 0;
        RuleScope = Scopes.FirstOrDefault();
        RuleMethod = Methods.FirstOrDefault();
        RuleProduct = null;
        RuleCategory = null;
        RuleValue = 0;
        RulePriority = 0;
        RuleExcludeFromTotal = false;
        IsRuleOpen = true;
    }

    [RelayCommand]
    private void OpenEditRule(CashbackRuleDto rule)
    {
        IsRuleNew = false;
        _editRuleId = rule.Id;
        RuleScope = Scopes.FirstOrDefault(s => s.Value == rule.Scope);
        RuleMethod = Methods.FirstOrDefault(m => m.Value == rule.Method);
        RuleProduct = Products.FirstOrDefault(p => p.Id == rule.TargetId);
        RuleCategory = Categories.FirstOrDefault(c => c.Id == rule.TargetId);
        RuleValue = rule.Value;
        RulePriority = rule.Priority;
        RuleExcludeFromTotal = rule.ExcludeFromTotalPercent;
        IsRuleOpen = true;
    }

    [RelayCommand]
    private void CancelRule() => IsRuleOpen = false;

    [RelayCommand]
    private async Task SaveRuleAsync()
    {
        var scope = RuleScope?.Value ?? "Product";
        long targetId = scope == "Product" ? RuleProduct?.Id ?? 0 : RuleCategory?.Id ?? 0;
        if (targetId == 0) { _toast.Warning(L["error"]); return; }
        if (RuleValue <= 0) { _toast.Warning(L["error"]); return; }

        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var method = RuleMethod?.Value ?? "Percent";
                if (IsRuleNew)
                    await _api.CreateRuleAsync(new CreateCashbackRuleRequest(scope, targetId, method, RuleValue, RulePriority, RuleExcludeFromTotal));
                else
                    await _api.UpdateRuleAsync(_editRuleId, new UpdateCashbackRuleRequest(scope, targetId, method, RuleValue, RulePriority, RuleExcludeFromTotal));
                await ReloadProgramAsync();
            }
            IsRuleOpen = false;
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task DeleteRuleAsync(CashbackRuleDto rule)
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                await _api.DeleteRuleAsync(rule.Id);
                await ReloadProgramAsync();
            }
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void OpenCreateDiscount()
    {
        IsDiscountNew = true;
        _editDiscountId = 0;
        DiscountName = "";
        DiscountEnabled = true;
        DiscountScope = DiscountScopes.FirstOrDefault();
        DiscountMethod = DiscountMethods.FirstOrDefault();
        DiscountProduct = null;
        DiscountCategory = null;
        DiscountManufacturer = null;
        DiscountCustomer = null;
        DiscountMinAmount = 0;
        DiscountValue = 0;
        DiscountPriority = 0;
        DiscountStartsOn = null;
        DiscountEndsOn = null;
        ExceptionScope = ExceptionScopes.FirstOrDefault();
        DiscountExceptions.Clear();
        IsDiscountOpen = true;
    }

    [RelayCommand]
    private async Task OpenEditDiscount(DiscountRow row)
    {
        var r = row.Dto;
        IsDiscountNew = false;
        _editDiscountId = r.Id;
        DiscountName = r.Name;
        DiscountEnabled = r.IsEnabled;
        DiscountScope = DiscountScopes.FirstOrDefault(s => s.Value == r.Scope);
        DiscountMethod = DiscountMethods.FirstOrDefault(m => m.Value == r.Method);
        DiscountProduct = r.Scope == "Product" ? Products.FirstOrDefault(p => p.Id == r.TargetId) : null;
        DiscountCategory = r.Scope == "Category" ? Categories.FirstOrDefault(c => c.Id == r.TargetId) : null;
        DiscountManufacturer = r.Scope == "Manufacturer" ? Manufacturers.FirstOrDefault(m => m.Id == r.TargetId) : null;
        DiscountCustomer = null;
        if (r.CustomerId is { } customerId)
        {
            try { DiscountCustomer = await _customersApi.GetByIdAsync(customerId); }
            catch { }
        }
        DiscountMinAmount = r.MinAmount;
        DiscountValue = r.Value;
        DiscountPriority = r.Priority;
        DiscountStartsOn = r.StartsOn?.ToDateTime(TimeOnly.MinValue);
        DiscountEndsOn = r.EndsOn?.ToDateTime(TimeOnly.MinValue);
        ExceptionScope = ExceptionScopes.FirstOrDefault();
        DiscountExceptions.Clear();
        foreach (var ex in r.Exceptions)
            DiscountExceptions.Add(ToChip(ex.Scope, ex.TargetId, ex.TargetName));
        IsDiscountOpen = true;
    }

    private ExceptionChip ToChip(string scope, long targetId, string name)
    {
        var kind = scope switch
        {
            "Category" => L["category"],
            "Manufacturer" => L["manufacturer"],
            _ => L["product"]
        };
        return new ExceptionChip(scope, targetId, $"{name} ({kind})");
    }

    [RelayCommand]
    private void CancelDiscount() => IsDiscountOpen = false;

    [RelayCommand]
    private void ClearDiscountCustomer() => DiscountCustomer = null;

    [RelayCommand]
    private void AddException()
    {
        (string Scope, long Id, string Name)? picked = ExceptionScope?.Value switch
        {
            "Category" when ExceptionCategory is { } c => ("Category", c.Id, c.Name),
            "Manufacturer" when ExceptionManufacturer is { } m => ("Manufacturer", m.Id, m.Name),
            _ when ExceptionProduct is { } p => ("Product", p.Id, p.Name),
            _ => null
        };
        if (picked is { } value && DiscountExceptions.All(e => e.Scope != value.Scope || e.TargetId != value.Id))
            DiscountExceptions.Add(ToChip(value.Scope, value.Id, value.Name));
        ExceptionProduct = null;
        ExceptionCategory = null;
        ExceptionManufacturer = null;
    }

    [RelayCommand]
    private void RemoveException(ExceptionChip chip) => DiscountExceptions.Remove(chip);

    private SaveDiscountRuleRequest BuildDiscountRequest(long id)
    {
        var scope = DiscountScope?.Value ?? "All";
        long? targetId = scope switch
        {
            "Product" => DiscountProduct?.Id,
            "Category" => DiscountCategory?.Id,
            "Manufacturer" => DiscountManufacturer?.Id,
            _ => null
        };
        return new SaveDiscountRuleRequest(id, DiscountName.Trim(), DiscountEnabled, scope, targetId,
            DiscountCustomer?.Id, DiscountMinAmount, DiscountMethod?.Value ?? "Percent", DiscountValue, DiscountPriority,
            DiscountStartsOn is { } s ? DateOnly.FromDateTime(s) : null,
            DiscountEndsOn is { } e ? DateOnly.FromDateTime(e) : null,
            DiscountExceptions.Select(c => new DiscountExceptionInputDto(c.Scope, c.TargetId)).ToList());
    }

    [RelayCommand]
    private async Task SaveDiscountAsync()
    {
        if (string.IsNullOrWhiteSpace(DiscountName) || DiscountValue <= 0) { _toast.Warning(L["error"]); return; }
        var scope = DiscountScope?.Value ?? "All";
        if (scope != "All" && BuildDiscountRequest(0).TargetId is null) { _toast.Warning(L["discount_target_required"]); return; }

        try
        {
            using (_busy.Begin(L["loading"]))
            {
                await _api.SaveDiscountRuleAsync(BuildDiscountRequest(IsDiscountNew ? 0 : _editDiscountId));
                await ReloadDiscountsAsync();
            }
            IsDiscountOpen = false;
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task ToggleDiscountAsync(DiscountRow row)
    {
        var r = row.Dto;
        try
        {
            await _api.SaveDiscountRuleAsync(new SaveDiscountRuleRequest(r.Id, r.Name, !r.IsEnabled, r.Scope, r.TargetId,
                r.CustomerId, r.MinAmount, r.Method, r.Value, r.Priority, r.StartsOn, r.EndsOn,
                r.Exceptions.Select(e => new DiscountExceptionInputDto(e.Scope, e.TargetId)).ToList()));
            await ReloadDiscountsAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task DeleteDiscountAsync(DiscountRow row)
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                await _api.DeleteDiscountRuleAsync(row.Dto.Id);
                await ReloadDiscountsAsync();
            }
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
