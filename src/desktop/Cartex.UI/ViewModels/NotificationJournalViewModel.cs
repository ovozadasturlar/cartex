using System.Collections.ObjectModel;
using Cartex.ApiClient.Api;
using Cartex.ApiClient.Paging;
using Cartex.ApiClient.Querying;
using Cartex.Shared.Models.Notifications;
using Cartex.UI.Services;
using Cartex.UI.ViewModels.Common;
using Cartex.UI.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cartex.UI.ViewModels;

public partial class NotificationJournalViewModel : ViewModelBase, ILoadable
{
    private readonly INotificationsApi _api;
    private readonly IToastService _toast;
    private readonly IBusyService _busy;
    private readonly IExportService _export;
    private readonly IDialogService _dialog;

    public ObservableCollection<NotificationDeliveryDto> Deliveries { get; } = [];
    public ObservableCollection<string> ChannelOptions { get; } = ["—"];
    public ObservableCollection<string> ProviderOptions { get; } = ["—"];
    public ObservableCollection<string> StatusOptions { get; } = ["—"];
    public ObservableCollection<string> PurposeOptions { get; } = ["—"];
    public PaginationState Paging { get; } = new();

    [ObservableProperty] private DateTimeOffset _dateFrom = DateTimeOffset.Now.AddDays(-30);
    [ObservableProperty] private DateTimeOffset _dateTo = DateTimeOffset.Now;
    [ObservableProperty] private string? _selectedChannel;
    [ObservableProperty] private string? _selectedProvider;
    [ObservableProperty] private string? _selectedStatus;
    [ObservableProperty] private string? _selectedPurpose;
    [ObservableProperty] private int _totalDeliveries;
    [ObservableProperty] private int _totalAttempts;
    [ObservableProperty] private int _totalAccepted;
    [ObservableProperty] private int _totalDelivered;
    [ObservableProperty] private int _totalUndelivered;
    [ObservableProperty] private int _totalFailed;
    [ObservableProperty] private int _totalSkipped;
    [ObservableProperty] private int _billableUnits;

    public bool IsEmpty => Deliveries.Count == 0;
    public bool CanExport { get; }

    public void SelectSmsChannel() => SelectedChannel = "Sms";

    public NotificationJournalViewModel(
        INotificationsApi api,
        IToastService toast,
        IBusyService busy,
        IExportService export,
        IDialogService dialog,
        AuthService auth)
    {
        _api = api;
        _toast = toast;
        _busy = busy;
        _export = export;
        _dialog = dialog;
        CanExport = auth.HasPermission("notifications.journal.export");
        Paging.Attach(LoadAsync);
        Paging.ConfigureSort([new(L["date"], "CreatedAt")], new(L["date"], "CreatedAt"));
        Paging.Descending = true;
    }

    public async Task LoadAsync()
    {
        try
        {
            using (_busy.Begin(L["loading"]))
            {
                await EnsureOptionsAsync();
                var (from, to) = Range();
                var queryTask = _api.QueryAsync(QueryRequest.Create()
                    .Page(Paging.Page, Paging.PageSize)
                    .Sort(Paging.SortBy, Paging.Descending)
                    .With("from", from)
                    .With("to", to)
                    .With("channel", Norm(SelectedChannel))
                    .With("provider", Norm(SelectedProvider))
                    .With("status", Norm(SelectedStatus))
                    .With("purpose", Norm(SelectedPurpose))
                    .Build());
                var statsTask = _api.GetStatsAsync(from, to, Norm(SelectedChannel), Norm(SelectedProvider), Norm(SelectedStatus), Norm(SelectedPurpose));
                await Task.WhenAll(queryTask, statsTask);

                var paged = queryTask.Result.ToPaged();
                Deliveries.Clear();
                foreach (var item in paged.Items) Deliveries.Add(item);
                Paging.Apply(paged.Meta);
                ApplyStats(statsTask.Result);
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private async Task EnsureOptionsAsync()
    {
        if (ChannelOptions.Count > 1)
            return;
        var options = await _api.GetOptionsAsync();
        AddOptions(ChannelOptions, options.Channels);
        AddOptions(ProviderOptions, options.Providers);
        AddOptions(StatusOptions, options.Statuses);
        AddOptions(PurposeOptions, options.Purposes);
    }

    private static void AddOptions(ObservableCollection<string> target, IEnumerable<string> values)
    {
        foreach (var value in values)
            target.Add(value);
    }

    private void ApplyStats(NotificationStatsDto stats)
    {
        TotalDeliveries = stats.Deliveries;
        TotalAttempts = stats.Attempts;
        TotalAccepted = stats.Accepted;
        TotalDelivered = stats.Delivered;
        TotalUndelivered = stats.Undelivered;
        TotalFailed = stats.Failed;
        TotalSkipped = stats.Skipped;
        BillableUnits = stats.BillableUnits;
    }

    [RelayCommand]
    private Task Refresh()
    {
        Paging.Page = 1;
        return LoadAsync();
    }

    [RelayCommand]
    private async Task OpenDetail(NotificationDeliveryDto delivery) =>
        await _dialog.ShowAsync<NotificationDeliveryDetailDialog, NotificationDeliveryDetailViewModel, object>(
            new NotificationDeliveryDetailViewModel(delivery));

    [RelayCommand]
    private async Task Export(string format)
    {
        if (!CanExport)
            return;
        try
        {
            var (from, to) = Range();
            var rows = await _api.GetAllAsync(0, 0, from, to, Norm(SelectedChannel), Norm(SelectedProvider), Norm(SelectedStatus), Norm(SelectedPurpose));
            var exportFormat = Enum.Parse<ExportFormat>(format);
            await _export.ExportAsync(L["notification_journal"], rows,
            [
                new(L["time"], x => x.CreatedAt),
                new(L["channel"], x => x.Channel),
                new(L["provider"], x => x.Provider),
                new(L["status"], x => x.Status),
                new(L["purpose"], x => x.Purpose),
                new(L["customer"], x => x.CustomerName),
                new(L["recipient"], x => x.Recipient),
                new(L["provider_message_id"], x => x.ProviderMessageId),
                new(L["attempts"], x => x.AttemptCount),
                new(L["billable_units"], x => x.Units),
                new(L["accepted_at"], x => x.AcceptedAt),
                new(L["delivered_at"], x => x.DeliveredAt),
                new(L["error"], x => x.Error)
            ], exportFormat);
            await _api.RecordExportAsync(new RecordNotificationExportRequest(
                from, to, Norm(SelectedChannel), Norm(SelectedProvider), Norm(SelectedStatus), Norm(SelectedPurpose), format, rows.Count));
        }
        catch (Exception ex) { _toast.Error(ApiErrors.Describe(ex)); }
    }

    private (DateTime From, DateTime To) Range() =>
        (new DateTimeOffset(DateFrom.Date).UtcDateTime, new DateTimeOffset(DateTo.Date.AddDays(1)).UtcDateTime);

    private static string? Norm(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == "—" ? null : value;
}
