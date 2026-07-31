using BarcodeScanning;
using Cartex.Mobile.Agent.ViewModels;
using Cartex.Mobile.Core;

namespace Cartex.Mobile.Agent.Views;

public partial class ScanPage : ContentPage
{
    private readonly ScanViewModel _vm;

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
        if (string.IsNullOrEmpty(value)) return;
        MainThread.BeginInvokeOnMainThread(() => _ = _vm.HandleAsync(value));
    }
}
