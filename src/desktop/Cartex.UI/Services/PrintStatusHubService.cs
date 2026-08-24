using Avalonia.Threading;
using Cartex.Shared.Models.Printing;
using Microsoft.AspNetCore.SignalR.Client;

namespace Cartex.UI.Services;

public sealed class PrintStatusHubService
{
    private readonly AuthService _auth;
    private readonly IToastService _toast;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private HubConnection? _connection;

    public PrintStatusHubService(AuthService auth, IToastService toast)
    {
        _auth = auth;
        _toast = toast;
        _auth.LoggedOut += Stop;
    }

    public async Task EnsureStartedAsync()
    {
        if (!_auth.HasPermission("printing.remote.use")
            || !SettingsService.Instance.IsFeatureOn("remote_printing")
            || string.IsNullOrWhiteSpace(_auth.DeviceId)) return;
        await _startLock.WaitAsync();
        try
        {
            _connection ??= Build();
            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync();
                await _connection.InvokeAsync("SubscribeRequester", _auth.DeviceId);
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
                options.AccessTokenProvider = () => _auth.EnsureFreshTokenAsync(CancellationToken.None);
                options.Headers["X-Client"] = "desktop";
                options.Headers["X-Device-Id"] = _auth.DeviceId;
                options.Headers["X-Device-Name"] = _auth.DeviceName;
            })
            .WithAutomaticReconnect()
            .Build();
        connection.On<PrintJobStatusUpdate>("PrintJobStatusChanged", update =>
            Dispatcher.UIThread.Post(() => Show(update)));
        connection.Reconnected += async _ =>
        {
            await connection.InvokeAsync("SubscribeRequester", _auth.DeviceId);
        };
        return connection;
    }

    private void Show(PrintJobStatusUpdate update)
    {
        var kind = PrintNotificationText.Kind(update.Kind);
        var printer = string.IsNullOrWhiteSpace(update.PrinterName) ? string.Empty : $" · {update.PrinterName}";
        if (update.Status == PrintJobStatus.Completed)
            _toast.Success(string.Format(LocalizationManager.Instance["print_completed"], kind, printer));
        else if (update.Status == PrintJobStatus.ManualReview)
            _toast.Warning(string.Format(LocalizationManager.Instance["print_result_unknown"], kind, update.ErrorMessage ?? string.Empty));
        else if (update.Status is PrintJobStatus.Failed or PrintJobStatus.Cancelled or PrintJobStatus.Rejected)
            _toast.Error(string.Format(LocalizationManager.Instance["print_failed"], kind, update.ErrorMessage ?? string.Empty));
    }

    private void Stop() => _ = StopAsync();

    private async Task StopAsync()
    {
        var connection = _connection;
        _connection = null;
        if (connection is null) return;
        try
        {
            await connection.DisposeAsync();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }
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
