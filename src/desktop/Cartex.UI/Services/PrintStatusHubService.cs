using Avalonia.Threading;
using Cartex.Shared.Models.Printing;
using Microsoft.AspNetCore.SignalR.Client;

namespace Cartex.UI.Services;

public sealed class PrintStatusHubService(AuthService auth, IToastService toast)
{
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private HubConnection? _connection;

    public async Task EnsureStartedAsync()
    {
        if (!auth.HasPermission("printing.remote.use") || string.IsNullOrWhiteSpace(auth.DeviceId)) return;
        await _startLock.WaitAsync();
        try
        {
            _connection ??= Build();
            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync();
                await _connection.InvokeAsync("SubscribeRequester", auth.DeviceId);
            }
        }
        catch
        {
        }
        finally
        {
            _startLock.Release();
        }
    }

    private HubConnection Build()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(SettingsService.Instance.ApiBaseUrl.TrimEnd('/') + "/hubs/printing", options =>
            {
                options.AccessTokenProvider = () => auth.EnsureFreshTokenAsync(CancellationToken.None);
                options.Headers["X-Client"] = "desktop";
                options.Headers["X-Device-Id"] = auth.DeviceId;
                options.Headers["X-Device-Name"] = auth.DeviceName;
            })
            .WithAutomaticReconnect()
            .Build();
        connection.On<PrintJobStatusUpdate>("PrintJobStatusChanged", update =>
            Dispatcher.UIThread.Post(() => Show(update)));
        connection.Reconnected += async _ =>
        {
            await connection.InvokeAsync("SubscribeRequester", auth.DeviceId);
        };
        return connection;
    }

    private void Show(PrintJobStatusUpdate update)
    {
        var kind = PrintNotificationText.Kind(update.Kind);
        var printer = string.IsNullOrWhiteSpace(update.PrinterName) ? string.Empty : $" · {update.PrinterName}";
        if (update.Status == PrintJobStatus.Completed)
            toast.Success(string.Format(LocalizationManager.Instance["print_completed"], kind, printer));
        else if (update.Status == PrintJobStatus.ManualReview)
            toast.Warning(string.Format(LocalizationManager.Instance["print_result_unknown"], kind, update.ErrorMessage ?? string.Empty));
        else if (update.Status is PrintJobStatus.Failed or PrintJobStatus.Cancelled or PrintJobStatus.Rejected)
            toast.Error(string.Format(LocalizationManager.Instance["print_failed"], kind, update.ErrorMessage ?? string.Empty));
    }
}

internal static class PrintNotificationText
{
    public static string Kind(PrintJobKind kind) => kind switch
    {
        PrintJobKind.Receipt => LocalizationManager.Instance["print_kind_receipt"],
        PrintJobKind.BarcodeLabel => LocalizationManager.Instance["print_kind_barcode"],
        PrintJobKind.ZReport => LocalizationManager.Instance["print_kind_zreport"],
        PrintJobKind.CartProforma => LocalizationManager.Instance["print_kind_preview"],
        _ => LocalizationManager.Instance["print_kind_document"]
    };
}
