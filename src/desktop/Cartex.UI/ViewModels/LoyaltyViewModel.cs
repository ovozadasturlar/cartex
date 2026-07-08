using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.Categories;
using Cartex.Shared.Models.Loyalty;
using Cartex.Shared.Models.Products;
using Cartex.UI.Models;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public partial class LoyaltyViewModel : ViewModelBase, ILoadable
{
    private readonly ILoyaltyApi _api;
    private readonly IProductsApi _productsApi;
    private readonly ICategoriesApi _categoriesApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private long _editRuleId;

    public ObservableCollection<LabeledValue> Scopes { get; } = [];
    public ObservableCollection<LabeledValue> Methods { get; } = [];
    public ObservableCollection<CashbackRuleDto> Rules { get; } = [];
    public ObservableCollection<ProductDto> Products { get; } = [];
    public ObservableCollection<CategoryDto> Categories { get; } = [];

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private decimal _totalPercent;
    [ObservableProperty] private int _roundingIndex;
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

    public bool IsProductScope => RuleScope?.Value == "Product";
    public bool RulesEmpty => Rules.Count == 0;

    public LoyaltyViewModel(ILoyaltyApi api, IProductsApi productsApi, ICategoriesApi categoriesApi, IToastService toast, IBusyService busy)
    {
        _api = api;
        _productsApi = productsApi;
        _categoriesApi = categoriesApi;
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

                await ReloadProgramAsync();
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
        Rules.Clear();
        foreach (var r in p.Rules) Rules.Add(r);
        OnPropertyChanged(nameof(RulesEmpty));
    }

    private void BuildLists()
    {
        if (Scopes.Count > 0) return;
        Scopes.Add(new LabeledValue("Product", L["product"]));
        Scopes.Add(new LabeledValue("Category", L["category"]));
        Methods.Add(new LabeledValue("Percent", L["cashback_method_percent"]));
        Methods.Add(new LabeledValue("FixedPerUnit", L["cashback_method_fixed"]));
    }

    partial void OnRuleScopeChanged(LabeledValue? value) => OnPropertyChanged(nameof(IsProductScope));

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.UpdateAsync(new UpdateLoyaltyProgramRequest(IsEnabled, TotalPercent, RoundingValues[Math.Clamp(RoundingIndex, 0, 3)]));
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
}
