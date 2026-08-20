using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.Accounts;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;

namespace Cartex.UI.ViewModels;

public partial class AccountsViewModel : ViewModelBase, ILoadable
{
    private readonly IAccountsApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly AuthService _auth;

    public ObservableCollection<AccountDto> Accounts { get; } = [];
    public PaginationState Paging { get; } = new();
    [ObservableProperty] private AccountsTotalsDto _totals = new(0, 0);
    [ObservableProperty] private string _searchText = "";
    public bool IsEmpty => Accounts.Count == 0;
    public bool CanExport => _auth.HasPermission("reports.export");

    public AccountsViewModel(IAccountsApi api, IToastService toast, IBusyService busy, IExportService export, AuthService auth)
    {
        _api = api;
        _toast = toast;
        _busy = busy;
        _export = export;
        _auth = auth;
        Paging.Attach(LoadAsync);
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
                var pagedTask = _api.QueryAsync(QueryRequest.Create()
                    .Page(Paging.Page, Paging.PageSize)
                    .Sort(Paging.SortBy, Paging.Descending)
                    .Search(search)
                    .Build());
                var totalsTask = _api.GetTotalsAsync(search);
                var paged = (await pagedTask).ToPaged();
                Accounts.Clear();
                foreach (var a in paged.Items) Accounts.Add(a);
                Paging.Apply(paged.Meta);
                Totals = await totalsTask;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private CancellationTokenSource? _searchCts;

    partial void OnSearchTextChanged(string value)
    {
        var cts = Debounce.Restart(ref _searchCts);
        _ = DebouncedSearchAsync(cts.Token);
    }

    private async Task DebouncedSearchAsync(CancellationToken token)
    {
        try { await Task.Delay(300, token); } catch { return; }
        if (token.IsCancellationRequested) return;
        Paging.Page = 1;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            var response = await _api.QueryAsync(QueryRequest.Create()
                .Page(0, 0)
                .Sort(Paging.SortBy, Paging.Descending)
                .Search(search)
                .Build());
            var all = response.Content ?? [];
            await _export.ExportAsync(L["accounts"], all,
            [
                new(L["name"], a => a.Name),
                new(L["type"], a => a.Type),
                new(L["balance"], a => a.Balance),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }
}
