using BarcodeScanning;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class ScanView : ContentView, ISectionView
{
    private static bool _cameraPermissionGranted;

    private readonly ScanViewModel _vm;

    public ScanView(ScanViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    public async void Appear()
    {
        if (!_cameraPermissionGranted && !(_cameraPermissionGranted = await Methods.AskForRequiredPermissionAsync()))
        {
            if (Shell.Current?.CurrentPage is { } page)
                await page.DisplayAlertAsync(Loc.Instance["camera_title"], Loc.Instance["camera_permission"], Loc.Instance["ok"]);
            MainPage.Current?.Show(0);
            return;
        }

        _vm.Appear();
        Reader.CameraEnabled = true;
        _ = _vm.RefreshVisibleProductAsync();
    }

    public void Disappear()
    {
        // Avval chiroq o'chiriladi (bog'lanish orqali), keyin kamera yopiladi: kamera seansi
        // chiroq yonib turganda uzilsa, ba'zi qurilmalarda chiroq yonib qolaveradi.
        _vm.Disappear();
        Reader.CameraEnabled = false;
        HideProductActionsImmediately();
    }

    public bool HandleBack()
    {
        if (_vm.OverlayVisible)
        {
            if (_vm.IsBarcodeMode)
            {
                _vm.BackToProductCommand.Execute(null);
                return true;
            }
            _vm.CloseOverlayCommand.Execute(null);
            return true;
        }

        if (_vm.UnknownBarcodeVisible)
        {
            _vm.CloseUnknownBarcodeCommand.Execute(null);
            return true;
        }

        if (_vm.SearchOpen)
        {
            _vm.CloseSearchCommand.Execute(null);
            return true;
        }

        return false;
    }

    private void OnDetectionFinished(object? sender, OnDetectionFinishedEventArg e)
    {
        foreach (var result in e.BarcodeResults)
        {
            if (!string.IsNullOrEmpty(result.RawValue))
                _ = _vm.HandleAsync(result.RawValue);
            break;
        }
    }

    private async void OnProductActionToggleClicked(object? sender, EventArgs e)
    {
        if (_vm.ProductActionsExpanded)
        {
            _vm.ProductActionsExpanded = false;
            return;
        }

        ProductActionMenu.IsVisible = true;
        ProductActionMenu.Opacity = 0;
        ProductActionMenu.TranslationY = -12;
        _vm.ProductActionsExpanded = true;
        await Task.WhenAll(ProductActionMenu.FadeToAsync(1, 170), ProductActionMenu.TranslateToAsync(0, 0, 170, Easing.CubicOut));
    }

    private void OnBarcodeActionClicked(object? sender, EventArgs e)
    {
        if (ProductActionMenu.IsVisible)
            _vm.ProductActionsExpanded = false;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScanViewModel.ProductActionsExpanded) && !_vm.ProductActionsExpanded)
            MainThread.BeginInvokeOnMainThread(() => _ = CollapseProductActionsAsync());
        if (e.PropertyName == nameof(ScanViewModel.OverlayVisible) && MainPage.Current is { } page)
            page.BarVisible = !_vm.OverlayVisible;
    }

    private async Task CollapseProductActionsAsync()
    {
        if (!ProductActionMenu.IsVisible) return;
        try
        {
            await Task.WhenAll(
                ProductActionMenu.FadeToAsync(0, 140),
                ProductActionMenu.TranslateToAsync(0, -12, 140, Easing.CubicIn));
        }
        catch
        {
            // Best-effort animatsiya: uzilib qolsa menyu shunchaki fade'siz yashiriladi.
        }
        if (!_vm.ProductActionsExpanded)
            ProductActionMenu.IsVisible = false;
    }

    private void HideProductActionsImmediately()
    {
        _vm.ProductActionsExpanded = false;
        ProductActionMenu.AbortAnimation("FadeTo");
        ProductActionMenu.AbortAnimation("TranslateTo");
        ProductActionMenu.Opacity = 0;
        ProductActionMenu.TranslationY = -12;
        ProductActionMenu.IsVisible = false;
    }

    private void OnQuantityEntryCompleted(object? sender, EventArgs e)
    {
        _vm.SetQuantityFromTextCommand.Execute(null);
        KeyboardDismissal.Hide();
    }

    private void OnSearchCompleted(object? sender, EventArgs e) => KeyboardDismissal.Hide();

    private void OnSearchFocused(object? sender, FocusEventArgs e) => _vm.SetSearchFocused(true);

    private void OnSearchUnfocused(object? sender, FocusEventArgs e) => _vm.SetSearchFocused(false);

    private void OnQuantityEntryUnfocused(object? sender, FocusEventArgs e) =>
        _vm.SetQuantityFromTextCommand.Execute(null);
}
