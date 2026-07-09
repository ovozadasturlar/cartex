using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Categories;
using Cartex.Shared.Models.Customers;
using Cartex.Shared.Models.Loyalty;
using Cartex.Shared.Models.Products;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public sealed record DiscountRow(DiscountRuleDto Dto, string ScopeText, string ValueText, string ConditionText, string PeriodText, string StatusText, bool IsEnabled);

public partial class LoyaltyViewModel : ViewModelBase, ILoadable
{
    private readonly ILoyaltyApi _api;
    private readonly IProductsApi _productsApi;
    private readonly ICategoriesApi _categoriesApi;
    private readonly ICustomersApi _customersApi;
    private readonly IManufacturersApi _manufacturersApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private long _editRuleId;
    private long _editDiscountId;

    public ObservableCollection<LabeledValue> Scopes { get; } = [];
    public ObservableCollection<LabeledValue> Methods { get; } = [];
    public ObservableCollection<CashbackRuleDto> Rules { get; } = [];
    public ObservableCollection<ProductDto> Products { get; } = [];
    public ObservableCollection<CategoryDto> Categories { get; } = [];
    public ObservableCollection<CustomerDto> Customers { get; } = [];
    public ObservableCollection<ManufacturerDto> Manufacturers { get; } = [];
    public ObservableCollection<DiscountRow> DiscountRules { get; } = [];
    public ObservableCollection<LabeledValue> DiscountScopes { get; } = [];
    public ObservableCollection<LabeledValue> DiscountMethods { get; } = [];
    public ObservableCollection<ProductDto> DiscountExceptions { get; } = [];

    [ObservableProperty] private string _sectionKey = "discounts";
    public bool IsDiscountSection => SectionKey == "discounts";
    public bool IsCashbackSection => SectionKey == "cashback";
    public bool IsSettingsSection => SectionKey == "settings";

    partial void OnSectionKeyChanged(string value)
    {
        OnPropertyChanged(nameof(IsDiscountSection));
        OnPropertyChanged(nameof(IsCashbackSection));
        OnPropertyChanged(nameof(IsSettingsSection));
    }

    [RelayCommand]
    private void SelectSection(string key) => SectionKey = key;

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
    [ObservableProperty] private ProductDto? _ruleProduct;
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
    [ObservableProperty] private ProductDto? _discountProduct;
    [ObservableProperty] private CategoryDto? _discountCategory;
    [ObservableProperty] private ManufacturerDto? _discountManufacturer;
    [ObservableProperty] private CustomerDto? _discountCustomer;
    [ObservableProperty] private decimal _discountMinAmount;
    [ObservableProperty] private decimal _discountValue;
    [ObservableProperty] private int _discountPriority;
    [ObservableProperty] private DateTimeOffset? _discountStartsOn;
    [ObservableProperty] private DateTimeOffset? _discountEndsOn;
    [ObservableProperty] private ProductDto? _exceptionCandidate;

    public bool IsProductScope => RuleScope?.Value == "Product";
    public bool RulesEmpty => Rules.Count == 0;
    public bool DiscountsEmpty => DiscountRules.Count == 0;
    public bool IsDiscountProductScope => DiscountScope?.Value == "Product";
    public bool IsDiscountCategoryScope => DiscountScope?.Value == "Category";
    public bool IsDiscountManufacturerScope => DiscountScope?.Value == "Manufacturer";

    public LoyaltyViewModel(ILoyaltyApi api, IProductsApi productsApi, ICategoriesApi categoriesApi,
        ICustomersApi customersApi, IManufacturersApi manufacturersApi, IToastService toast, IBusyService busy)
    {
        _api = api;
        _productsApi = productsApi;
        _categoriesApi = categoriesApi;
        _customersApi = customersApi;
        _manufacturersApi = manufacturersApi;
        _toast = toast;
        _busy = busy;
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                BuildLists();

                var products = await _productsApi.GetAllAsync();
                Products.Clear();
                foreach (var p in products) Products.Add(p);

                var categories = await _categoriesApi.GetAllAsync();
                Categories.Clear();
                foreach (var c in categories) Categories.Add(c);

                var customers = await _customersApi.GetAllAsync(null);
                Customers.Clear();
                foreach (var c in customers) Customers.Add(c);

                var manufacturers = await _manufacturersApi.GetAllAsync();
                Manufacturers.Clear();
                foreach (var m in manufacturers) Manufacturers.Add(m);

                await ReloadProgramAsync();
                await ReloadDiscountsAsync();
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task ReloadProgramAsync()
    {
        var p = await _api.GetAsync();
        IsEnabled = p.IsEnabled;
        RoundingIndex = Math.Max(0, Array.IndexOf(RoundingValues, p.CashbackRounding));
        TotalPercent = p.TotalPercent;
        CombineModeIndex = p.DiscountCombineMode == "Stack" ? 1 : 0;
        Rules.Clear();
        foreach (var r in p.Rules) Rules.Add(r);
        OnPropertyChanged(nameof(RulesEmpty));
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
        DiscountExceptions.Clear();
        IsDiscountOpen = true;
    }

    [RelayCommand]
    private void OpenEditDiscount(DiscountRow row)
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
        DiscountCustomer = Customers.FirstOrDefault(c => c.Id == r.CustomerId);
        DiscountMinAmount = r.MinAmount;
        DiscountValue = r.Value;
        DiscountPriority = r.Priority;
        DiscountStartsOn = r.StartsOn is { } s ? new DateTimeOffset(s.ToDateTime(TimeOnly.MinValue)) : null;
        DiscountEndsOn = r.EndsOn is { } e ? new DateTimeOffset(e.ToDateTime(TimeOnly.MinValue)) : null;
        DiscountExceptions.Clear();
        foreach (var ex in r.Exceptions)
        {
            var product = Products.FirstOrDefault(p => p.Id == ex.ProductId);
            if (product is not null) DiscountExceptions.Add(product);
        }
        IsDiscountOpen = true;
    }

    [RelayCommand]
    private void CancelDiscount() => IsDiscountOpen = false;

    [RelayCommand]
    private void ClearDiscountCustomer() => DiscountCustomer = null;

    [RelayCommand]
    private void AddException()
    {
        if (ExceptionCandidate is { } candidate && DiscountExceptions.All(p => p.Id != candidate.Id))
            DiscountExceptions.Add(candidate);
        ExceptionCandidate = null;
    }

    [RelayCommand]
    private void RemoveException(ProductDto product) => DiscountExceptions.Remove(product);

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
            DiscountStartsOn is { } s ? DateOnly.FromDateTime(s.Date) : null,
            DiscountEndsOn is { } e ? DateOnly.FromDateTime(e.Date) : null,
            DiscountExceptions.Select(p => p.Id).ToList());
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
                r.Exceptions.Select(e => e.ProductId).ToList()));
            await ReloadDiscountsAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [ObservableProperty] private string _newManufacturerName = "";

    [RelayCommand]
    private async Task AddManufacturerAsync()
    {
        if (string.IsNullOrWhiteSpace(NewManufacturerName)) return;
        try
        {
            await _manufacturersApi.CreateAsync(new SaveManufacturerRequest(NewManufacturerName.Trim()));
            NewManufacturerName = "";
            var manufacturers = await _manufacturersApi.GetAllAsync();
            Manufacturers.Clear();
            foreach (var m in manufacturers) Manufacturers.Add(m);
            _toast.Success(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task DeleteManufacturerAsync(ManufacturerDto manufacturer)
    {
        try
        {
            await _manufacturersApi.DeleteAsync(manufacturer.Id);
            Manufacturers.Remove(manufacturer);
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
