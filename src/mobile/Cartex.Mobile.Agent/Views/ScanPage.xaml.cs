using Cartex.Mobile.Agent.Services;
using Cartex.Mobile.Agent.ViewModels;
using ZXing.Net.Maui;

namespace Cartex.Mobile.Agent.Views;

public partial class ScanPage : ContentPage
{
    private readonly ScanViewModel _vm;

    public ScanPage(ScanViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        Reader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.QrCode,
            AutoRotate = true,
            TryHarder = true
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var status = await Permissions.RequestAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert(Loc.Instance["camera_title"], Loc.Instance["camera_permission"], Loc.Instance["ok"]);
            await Shell.Current.GoToAsync("..");
        }
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var value = e.Results.FirstOrDefault()?.Value;
        if (string.IsNullOrEmpty(value)) return;
        MainThread.BeginInvokeOnMainThread(() => _ = _vm.HandleAsync(value));
    }
}
