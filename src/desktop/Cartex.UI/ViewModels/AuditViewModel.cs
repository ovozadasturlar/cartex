using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Querying;
using Cartex.ApiClient.Paging;
using Cartex.Shared.Models.AuditLogs;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;

namespace Cartex.UI.ViewModels;

public partial class AuditViewModel : ViewModelBase, ILoadable
{
    private readonly IAuditLogsApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly AuthService _auth;

    public ObservableCollection<AuditLogDto> Logs { get; } = [];
    public PaginationState Paging { get; } = new();
    public bool IsEmpty => Logs.Count == 0;
    public bool CanExport => _auth.HasPermission("reports.export");

    [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddDays(-30);
    [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;
    [ObservableProperty] private string? _searchUser;
    [ObservableProperty] private string? _searchTable;
    [ObservableProperty] private string? _searchAction;

    public ObservableCollection<string> UserOptions { get; } = [];
    public ObservableCollection<string> TableOptions { get; } = [];
    public ObservableCollection<string> ActionOptions { get; } = [];

    private static string? Norm(string? value) => string.IsNullOrWhiteSpace(value) || value == "—" ? null : value;

    partial void OnSearchUserChanged(string? value) { Paging.Page = 1; _ = LoadAsync(); }
    partial void OnSearchTableChanged(string? value) { Paging.Page = 1; _ = LoadAsync(); }
    partial void OnSearchActionChanged(string? value) { Paging.Page = 1; _ = LoadAsync(); }

    private async Task EnsureOptionsAsync()
    {
        if (TableOptions.Count > 0) return;
        try
        {
            var options = await _api.GetOptionsAsync();
            TableOptions.Add("—");
            foreach (var t in options.Tables) TableOptions.Add(t);
            ActionOptions.Add("—");
            foreach (var a in options.Actions) ActionOptions.Add(a);
            UserOptions.Add("—");
            foreach (var u in options.Users) UserOptions.Add(u);
        }
        catch { }
    }

    public AuditViewModel(IAuditLogsApi api, IToastService toast, IBusyService busy, IExportService export, AuthService auth)
    {
        _api = api;
        _toast = toast;
        _busy = busy;
        _export = export;
        _auth = auth;
        Paging.Attach(LoadAsync);
        Paging.ConfigureSort([new(L["date"], "CreatedAt"), new(L["table_name"], "TableName"), new(L["action"], "Action")], new(L["date"], "CreatedAt"));
        Paging.Descending = true;
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        try
        {
            var from = new DateTimeOffset(DateFrom.Date).UtcDateTime;
            var to = new DateTimeOffset(DateTo.Date.AddDays(1)).UtcDateTime;
            var all = await _api.GetAllAsync(Norm(SearchTable), Norm(SearchUser), Norm(SearchAction), from, to);
            await _export.ExportAsync(L["audit"], all,
            [
                new(L["time"], a => a.CreatedAt),
                new(L["user"], a => a.UserName),
                new(L["table_name"], a => a.TableName),
                new(L["action"], a => a.Action),
                new(L["record_id"], a => a.RecordId),
            ], Enum.Parse<ExportFormat>(format));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                await EnsureOptionsAsync();
                var from = new DateTimeOffset(DateFrom.Date).UtcDateTime;
                var to = new DateTimeOffset(DateTo.Date.AddDays(1)).UtcDateTime;
                var result = await _api.QueryAsync(QueryRequest.Create()
                    .Page(Paging.Page, Paging.PageSize)
                    .Sort(Paging.SortBy, Paging.Descending)
                    .With("tableName", Norm(SearchTable))
                    .With("userName", Norm(SearchUser))
                    .With("action", Norm(SearchAction))
                    .With("fromDate", from)
                    .With("toDate", to)
                    .Build());
                var paged = result.ToPaged();
                Logs.Clear();
                foreach (var a in paged.Items) Logs.Add(a);
                Paging.Apply(paged.Meta);
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    [RelayCommand]
    private Task Refresh() { Paging.Page = 1; return LoadAsync(); }
}
