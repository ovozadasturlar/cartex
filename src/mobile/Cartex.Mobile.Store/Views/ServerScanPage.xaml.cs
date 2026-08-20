using BarcodeScanning;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Store.Views;

public partial class ServerScanPage : ContentPage
{
    private bool _handled;

    public ServerScanPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!await Methods.AskForRequiredPermissionAsync())
        {
            await DisplayAlertAsync(Loc.Instance["camera_title"], Loc.Instance["camera_permission"], Loc.Instance["ok"]);
            await Shell.Current.GoToAsync("..");
            return;
        }
        Reader.CameraEnabled = true;
    }

    protected override void OnDisappearing()
    {
        Reader.CameraEnabled = false;
        base.OnDisappearing();
    }

    private void OnDetectionFinished(object? sender, OnDetectionFinishedEventArg e)
    {
        var value = e.BarcodeResults.FirstOrDefault()?.RawValue;
        if (_handled || string.IsNullOrEmpty(value)) return;
        if (!QrActions.TryServerOrUrl(value, out var url)) return;
        _handled = true;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Reader.PauseScanning = true;
            _ = Shell.Current.GoToAsync("..", new ShellNavigationQueryParameters { ["server"] = url });
        });
    }
}
