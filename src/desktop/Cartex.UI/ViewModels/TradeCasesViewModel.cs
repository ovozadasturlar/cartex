using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.Shared.Models.TradeCases;
using Cartex.UI.Services;
using Cartex.UI.Views;

namespace Cartex.UI.ViewModels;

public partial class TradeCasesViewModel : ViewModelBase, ILoadable
{
    private readonly ITradeCasesApi _api;
    private readonly ICustomersApi _customersApi;
    private readonly NavigationService _navigation;
    private readonly IDialogService _dialog;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly AuthService _auth;
    private readonly BranchContextService _branch;

    public TradeCasesViewModel(ITradeCasesApi api, ICustomersApi customersApi, NavigationService navigation,
        IDialogService dialog, IToastService toast, IBusyService busy, AuthService auth, BranchContextService branch)
    {
        _api = api;
        _customersApi = customersApi;
        _navigation = navigation;
        _dialog = dialog;
        _toast = toast;
        _busy = busy;
        _auth = auth;
        _branch = branch;
        auth.LoggedOut += ResetState;
    }

    public ObservableCollection<TradeCaseListDto> Cases { get; } = [];

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _statusFilter = "";

    public bool IsEmpty => Cases.Count == 0;
    public bool CanCreate => _auth.HasPermission("trade_cases.create");
    public bool IsAllActive => StatusFilter == "";
    public bool IsOpenActive => StatusFilter == "Open";
    public bool IsPendingActive => StatusFilter == "SettlementPending";
    public bool IsSettledActive => StatusFilter == "Settled";
    public bool IsCancelledActive => StatusFilter == "Cancelled";

    private void ResetState()
    {
        _searchCts?.Cancel();
        Cases.Clear();
        SearchText = "";
        StatusFilter = "";
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void SetStatus(string status) => StatusFilter = status;

    partial void OnStatusFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsAllActive));
        OnPropertyChanged(nameof(IsOpenActive));
        OnPropertyChanged(nameof(IsPendingActive));
        OnPropertyChanged(nameof(IsSettledActive));
        OnPropertyChanged(nameof(IsCancelledActive));
        _ = LoadAsync();
    }

    private CancellationTokenSource? _searchCts;

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        _ = DebouncedSearchAsync(cts.Token);
    }

    private async Task DebouncedSearchAsync(CancellationToken token)
    {
        try { await Task.Delay(300, token); } catch { return; }
        if (!token.IsCancellationRequested) await LoadAsync();
    }

    public async Task LoadAsync()
    {
        OnPropertyChanged(nameof(CanCreate));
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
                var cases = await _api.GetAsync(status: string.IsNullOrEmpty(StatusFilter) ? null : StatusFilter, search: search);
                Cases.Clear();
                foreach (var c in cases) Cases.Add(c);
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync();

    [RelayCommand]
    private void OpenDetail(TradeCaseListDto row)
    {
        var vm = ServiceLocator.Resolve<TradeCaseDetailViewModel>();
        vm.Init(row.Id);
        _navigation.RequestPageNavigation(vm);
    }

    [RelayCommand]
    private async Task OpenCreateAsync()
    {
        if (!CanCreate) return;
        var result = await _dialog.ShowAsync<TradeCaseCreateDialog, TradeCaseCreateViewModel, TradeCaseCreatedDto>(
            new TradeCaseCreateViewModel(_customersApi, _api, _toast, _busy, _branch));
        if (result is null) return;
        _toast.Success(L["tc_created"]);
        var vm = ServiceLocator.Resolve<TradeCaseDetailViewModel>();
        vm.Init(result.Id);
        _navigation.RequestPageNavigation(vm);
    }
}
