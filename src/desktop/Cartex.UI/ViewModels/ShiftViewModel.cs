using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.ExpenseCategories;
using Cartex.Shared.Models.Shifts;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;

namespace Cartex.UI.ViewModels;

public partial class CurrencyCashRow : ObservableObject
{
    public string Currency { get; init; } = "";
    [ObservableProperty] private decimal _opening;
    [ObservableProperty] private decimal _counted;
}

public partial class ShiftViewModel : ViewModelBase, ILoadable
{
    private readonly IShiftsApi _api;
    private readonly IExpenseCategoriesApi _expenseApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly AuthService _auth;

    [ObservableProperty] private CurrentShiftDto? _current;
    [ObservableProperty] private decimal _openingFloat;
    [ObservableProperty] private decimal _movementAmount;
    [ObservableProperty] private string? _movementReason;
    [ObservableProperty] private ExpenseCategoryDto? _selectedExpenseCategory;
    [ObservableProperty] private decimal _countedCash;
    [ObservableProperty] private ZReportDto? _lastReport;
    [ObservableProperty] private bool _isReportOpen;

    public ObservableCollection<ShiftHistoryDto> History { get; } = [];
    public ObservableCollection<ExpenseCategoryDto> ExpenseCategories { get; } = [];
    public PaginationState HistoryPaging { get; } = new();

    public bool HasShift => Current is not null;
    public bool NoShift => Current is null;
    public bool HasReport => LastReport is not null;
    public bool CanViewHistory => _auth.HasPermission("shifts.view");

    [ObservableProperty] private bool _isMulticurrency;
    public ObservableCollection<CurrencyCashRow> CurrencyRows { get; } = [];
    private readonly IBusinessApi _businessApi;
    private readonly IRatesApi _ratesApi;
    private readonly ReferenceCache _cache;

    public ShiftViewModel(IShiftsApi api, IExpenseCategoriesApi expenseApi, IToastService toast, IBusyService busy, AuthService auth, IBusinessApi businessApi, IRatesApi ratesApi, ReferenceCache cache)
    {
        _businessApi = businessApi;
        _ratesApi = ratesApi;
        _cache = cache;
        _api = api;
        _expenseApi = expenseApi;
        _toast = toast;
        _busy = busy;
        _auth = auth;
        _auth.LoggedOut += ResetState;
        HistoryPaging.Attach(LoadHistoryAsync);
    }

    private void ResetState()
    {
        Current = null;
        LastReport = null;
        IsReportOpen = false;
        History.Clear();
        ExpenseCategories.Clear();
        CurrencyRows.Clear();
        IsMulticurrency = false;
        OpeningFloat = 0;
        MovementAmount = 0;
        MovementReason = null;
        SelectedExpenseCategory = null;
        CountedCash = 0;
    }

    private void RaisePermissions()
    {
        OnPropertyChanged(nameof(CanViewHistory));
    }

    public async Task LoadAsync()
    {
        RaisePermissions();
        try
        {
            var currentTask = _api.GetCurrentAsync();
            var historyTask = LoadHistoryAsync();
            var expensesTask = LoadExpenseCategoriesAsync();
            using (_busy.Begin(L["loading"]))
                Current = await currentTask;
            await LoadCurrenciesAsync();
            await Task.WhenAll(historyTask, expensesTask);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task LoadCurrenciesAsync()
    {
        try
        {
            var business = await _cache.GetAsync(CacheKeys.Business, _businessApi.GetAsync);
            IsMulticurrency = business.Multicurrency;
            if (!IsMulticurrency) { CurrencyRows.Clear(); return; }

            var rates = await _cache.GetAsync(CacheKeys.Rates, _ratesApi.GetCurrentAsync);
            CurrencyRows.Clear();
            foreach (var rate in rates.OrderBy(r => r.Code))
            {
                var current = Current?.Currencies.FirstOrDefault(c => c.Currency == rate.Code);
                CurrencyRows.Add(new CurrencyCashRow { Currency = rate.Code, Opening = current?.OpeningFloat ?? 0 });
            }
        }
        catch { }
    }

    private async Task LoadExpenseCategoriesAsync()
    {
        try
        {
            var items = await _cache.GetAsync(CacheKeys.ExpenseCategories, () => _expenseApi.GetAllAsync());
            ExpenseCategories.Clear();
            foreach (var c in items) ExpenseCategories.Add(c);
        }
        catch { }
    }

    private async Task LoadHistoryAsync()
    {
        if (!CanViewHistory) return;
        try
        {
            var result = await _api.GetHistoryAsync(HistoryPaging.Page, HistoryPaging.PageSize);
            var paged = result.ToPaged();
            History.Clear();
            foreach (var s in paged.Items) History.Add(s);
            HistoryPaging.Apply(paged.Meta);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }   

    [RelayCommand]
    private async Task ShowReport(ShiftHistoryDto shift)
    {
        try
        {
            using (_busy.Begin(L["loading"]))
                LastReport = await _api.GetReportAsync(shift.Id);
            IsReportOpen = true;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void CloseReport() => IsReportOpen = false;

    partial void OnCurrentChanged(CurrentShiftDto? value)
    {
        OnPropertyChanged(nameof(HasShift));
        OnPropertyChanged(nameof(NoShift));
    }

    partial void OnLastReportChanged(ZReportDto? value) => OnPropertyChanged(nameof(HasReport));

    [RelayCommand]
    private async Task OpenShiftAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
                await _api.OpenAsync(new OpenShiftRequest(OpeningFloat,
                    IsMulticurrency ? CurrencyRows.Where(r => r.Opening > 0).Select(r => new CurrencyAmountDto(r.Currency, r.Opening)).ToList() : null));
            LastReport = null;
            OpeningFloat = 0;
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task PayInAsync() => await MoveCashAsync(false);

    [RelayCommand]
    private async Task PayOutAsync() => await MoveCashAsync(true);

    private async Task MoveCashAsync(bool isPayOut)
    {
        if (MovementAmount <= 0) { _toast.Warning(L["error"]); return; }
        try
        {
            var expenseCategoryId = isPayOut ? SelectedExpenseCategory?.Id : null;
            using (_busy.Begin(L["loading"]))
                await _api.CashMovementAsync(new CashMovementRequest(MovementAmount, isPayOut, MovementReason, expenseCategoryId));
            MovementAmount = 0;
            MovementReason = null;
            SelectedExpenseCategory = null;
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private async Task CloseShiftAsync()
    {
        if (Current is null) return;
        try
        {
            ZReportDto report;
            using (_busy.Begin(L["loading"]))
                report = await _api.CloseAsync(Current.Id, new CloseShiftRequest(CountedCash,
                    IsMulticurrency ? CurrencyRows.Select(r => new CurrencyAmountDto(r.Currency, r.Counted)).ToList() : null));
            LastReport = report;
            IsReportOpen = true;
            CountedCash = 0;
            _toast.Success(L["success"]);
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); return; }

        var printer = ServiceLocator.Resolve<IPrinterService>();
        if (!printer.GetSettings().AutoPrintZReport || LastReport is null) return;
        try { printer.PrintZReport(LastReport); }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void PrintReport()
    {
        if (LastReport is null) return;
        try
        {
            ServiceLocator.Resolve<IPrinterService>().PrintZReport(LastReport);
            _toast.Info(L["success"]);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
