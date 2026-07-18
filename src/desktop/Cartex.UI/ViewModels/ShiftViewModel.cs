using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.Common;
using Cartex.Shared.Models.ExpenseCategories;
using Cartex.Shared.Models.Shifts;
using Cartex.Shared.Models.Users;
using Avalonia.Threading;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;

namespace Cartex.UI.ViewModels;

public partial class CurrencyCashRow : ObservableObject
{
    public string Currency { get; init; } = "";
    public decimal Expected { get; init; }
    [ObservableProperty] private decimal _opening;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Difference))] private decimal _counted;
    public decimal Difference => Counted - Expected;
}

public partial class ShiftViewModel : ViewModelBase, ILoadable
{
    private readonly IShiftsApi _api;
    private readonly IExpenseCategoriesApi _expenseApi;
    private readonly IUsersApi _usersApi;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IDialogService _dialog;
    private readonly AuthService _auth;
    private readonly DispatcherTimer _durationTimer;

    [ObservableProperty] private CurrentShiftDto? _current;
    [ObservableProperty] private decimal _openingFloat;
    [ObservableProperty] private decimal _movementAmount;
    [ObservableProperty] private string? _movementReason;
    [ObservableProperty] private ExpenseCategoryDto? _selectedExpenseCategory;
    [ObservableProperty] private decimal _countedCash;
    [ObservableProperty] private ZReportDto? _lastReport;
    [ObservableProperty] private bool _isReportOpen;
    [ObservableProperty] private UserDto? _selectedCashier;
    [ObservableProperty] private string? _baseCurrency;
    [ObservableProperty] private string? _reportMeta;

    public ObservableCollection<ShiftHistoryDto> History { get; } = [];
    public ObservableCollection<ExpenseCategoryDto> ExpenseCategories { get; } = [];
    public ObservableCollection<UserDto> Cashiers { get; } = [];
    public PaginationState HistoryPaging { get; } = new();

    public bool HasShift => Current is not null;
    public bool NoShift => Current is null;
    public decimal BaseDifference => CountedCash - (Current?.ExpectedCash ?? 0);

    public string? ShiftDuration
    {
        get
        {
            if (Current is null) return null;
            var span = DateTime.Now - Current.OpenedAt;
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            return span.TotalHours >= 1
                ? string.Format(L["shift_open_duration"], (int)span.TotalHours, span.Minutes)
                : string.Format(L["shift_open_duration_min"], span.Minutes);
        }
    }
    public bool HasReport => LastReport is not null;
    public bool CanViewHistory => _auth.HasPermission("shifts.view");
    public bool CanViewAll => _auth.HasPermission("shifts.viewAll");
    public bool CanManageAll => _auth.HasPermission("shifts.manageAll");

    [ObservableProperty] private bool _isMulticurrency;
    public ObservableCollection<CurrencyCashRow> CurrencyRows { get; } = [];
    private readonly IBusinessApi _businessApi;
    private readonly IRatesApi _ratesApi;
    private readonly ReferenceCache _cache;

    public ShiftViewModel(IShiftsApi api, IExpenseCategoriesApi expenseApi, IUsersApi usersApi, IToastService toast, IBusyService busy, IDialogService dialog, AuthService auth, IBusinessApi businessApi, IRatesApi ratesApi, ReferenceCache cache)
    {
        _businessApi = businessApi;
        _ratesApi = ratesApi;
        _cache = cache;
        _api = api;
        _expenseApi = expenseApi;
        _usersApi = usersApi;
        _toast = toast;
        _busy = busy;
        _dialog = dialog;
        _auth = auth;
        _auth.LoggedOut += ResetState;
        _durationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _durationTimer.Tick += (_, _) => OnPropertyChanged(nameof(ShiftDuration));
        HistoryPaging.Attach(LoadHistoryAsync);
    }

    private void ResetState()
    {
        Current = null;
        LastReport = null;
        ReportMeta = null;
        BaseCurrency = null;
        IsReportOpen = false;
        History.Clear();
        ExpenseCategories.Clear();
        Cashiers.Clear();
        _selectedCashier = null;
        OnPropertyChanged(nameof(SelectedCashier));
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
        OnPropertyChanged(nameof(CanViewAll));
        OnPropertyChanged(nameof(CanManageAll));
    }

    public async Task LoadAsync()
    {
        RaisePermissions();
        try
        {
            var currentTask = _api.GetCurrentAsync();
            var historyTask = LoadHistoryAsync();
            var expensesTask = LoadExpenseCategoriesAsync();
            var cashiersTask = LoadCashiersAsync();
            using (_busy.Begin(L["loading"]))
                Current = await currentTask;
            OnPropertyChanged(nameof(ShiftDuration));
            await LoadCurrenciesAsync();
            await Task.WhenAll(historyTask, expensesTask, cashiersTask);
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task LoadCurrenciesAsync()
    {
        try
        {
            var business = await _cache.GetAsync(CacheKeys.Business, _businessApi.GetAsync);
            BaseCurrency = business.Currency;
            IsMulticurrency = business.Multicurrency;
            CurrencyRows.Clear();
            if (!IsMulticurrency) return;

            var currencies = await _ratesApi.GetCurrenciesAsync(onlyEnabled: true);
            foreach (var c in currencies.Where(c => !c.IsBase).OrderBy(c => c.Code))
            {
                var current = Current?.Currencies.FirstOrDefault(x => x.Currency == c.Code);
                CurrencyRows.Add(new CurrencyCashRow { Currency = c.Code, Opening = current?.OpeningFloat ?? 0, Expected = current?.ExpectedCash ?? 0 });
            }
            foreach (var extra in Current?.Currencies ?? [])
                if ((extra.OpeningFloat != 0 || extra.ExpectedCash != 0) && CurrencyRows.All(r => r.Currency != extra.Currency))
                    CurrencyRows.Add(new CurrencyCashRow { Currency = extra.Currency, Opening = extra.OpeningFloat, Expected = extra.ExpectedCash });
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

    private async Task LoadCashiersAsync()
    {
        if (!CanViewAll || Cashiers.Count > 0) return;
        try
        {
            foreach (var u in await _usersApi.GetAllAsync()) Cashiers.Add(u);
        }
        catch { }
    }

    partial void OnSelectedCashierChanged(UserDto? value)
    {
        HistoryPaging.Page = 1;
        _ = LoadHistoryAsync();
    }

    [RelayCommand]
    private void ClearCashier() => SelectedCashier = null;

    private async Task LoadHistoryAsync()
    {
        if (!CanViewHistory) return;
        try
        {
            var result = await _api.GetHistoryAsync(HistoryPaging.Page, HistoryPaging.PageSize, SelectedCashier?.Id);
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
            ReportMeta = shift.ClosedAt is { } closed
                ? $"{shift.UserName} · {shift.OpenedAt:dd.MM.yyyy HH:mm} – {closed:dd.MM.yyyy HH:mm}"
                : $"{shift.UserName} · {shift.OpenedAt:dd.MM.yyyy HH:mm}";
            IsReportOpen = true;
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private void CloseReport() => IsReportOpen = false;

    [RelayCommand]
    private async Task ForceCloseAsync(ShiftHistoryDto shift)
    {
        try
        {
            var report = await _api.GetReportAsync(shift.Id);
            if (!await _dialog.ConfirmAsync(string.Format(L["force_close_confirm"], shift.UserName, report.ExpectedCash), L["force_close"])) return;
            using (_busy.Begin(L["loading"]))
                LastReport = await _api.CloseAsync(shift.Id, new CloseShiftRequest(report.ExpectedCash,
                    IsMulticurrency ? report.Currencies.Select(c => new CurrencyAmountDto(c.Currency, c.ExpectedCash)).ToList() : null));
            ReportMeta = $"{shift.UserName} · {shift.OpenedAt:dd.MM.yyyy HH:mm}";
            IsReportOpen = true;
            await LoadAsync();
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    partial void OnCurrentChanged(CurrentShiftDto? value)
    {
        OnPropertyChanged(nameof(HasShift));
        OnPropertyChanged(nameof(NoShift));
        OnPropertyChanged(nameof(BaseDifference));
        OnPropertyChanged(nameof(ShiftDuration));
        if (value is null) _durationTimer.Stop();
        else _durationTimer.Start();
    }

    partial void OnCountedCashChanged(decimal value) => OnPropertyChanged(nameof(BaseDifference));

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
        var diff = CountedCash - Current.ExpectedCash;
        if (diff != 0 && !await _dialog.ConfirmAsync(string.Format(L["close_shift_diff_confirm"], (diff > 0 ? "+" : "") + diff.ToString("N0")), L["close_shift"])) return;
        ReportMeta = $"{_auth.UserInfo?.FullName} · {Current.OpenedAt:dd.MM.yyyy HH:mm}";
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
