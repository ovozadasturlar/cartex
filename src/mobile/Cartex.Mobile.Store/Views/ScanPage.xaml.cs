using BarcodeScanning;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class ScanPage : ContentPage
{
    private readonly ScanViewModel _vm;
    private CancellationTokenSource? _cameraCts;

    public ScanPage(ScanViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!await Methods.AskForRequiredPermissionAsync())
        {
            await DisplayAlertAsync(Loc.Instance["camera_title"], Loc.Instance["camera_permission"], Loc.Instance["ok"]);
            await Shell.Current.GoToAsync("//home");
            return;
        }

        _cameraCts?.Cancel();
        _cameraCts = new CancellationTokenSource();
        await RestartCameraAsync(_cameraCts.Token);
    }

    protected override void OnDisappearing()
    {
        _cameraCts?.Cancel();
        Reader.CameraEnabled = false;
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_vm.OverlayVisible)
        {
            _vm.CloseOverlayCommand.Execute(null);
            return true;
        }

        if (_vm.UnknownBarcodeVisible)
        {
            _vm.CloseUnknownBarcodeCommand.Execute(null);
            return true;
        }

        if (_vm.SearchVisible)
        {
            _vm.ToggleSearchCommand.Execute(null);
            return true;
        }

        // Scanner is the root of this flow: do not close the app from here.
        return true;
    }

    private void OnDetectionFinished(object? sender, OnDetectionFinishedEventArg e)
    {
        var value = e.BarcodeResults.FirstOrDefault()?.RawValue;
        if (string.IsNullOrEmpty(value)) return;
        MainThread.BeginInvokeOnMainThread(() => _ = _vm.HandleAsync(value));
    }

    private async Task RestartCameraAsync(CancellationToken cancellationToken)
    {
        Reader.CameraEnabled = false;
        try { await Task.Delay(220, cancellationToken); }
        catch (OperationCanceledException) { return; }
        if (!cancellationToken.IsCancellationRequested)
            Reader.CameraEnabled = true;
    }
}
