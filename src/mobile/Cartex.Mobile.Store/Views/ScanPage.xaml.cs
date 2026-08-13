using BarcodeScanning;
using Cartex.Mobile.Core;
using Cartex.Mobile.Store.Services;
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
        _vm.PropertyChanged += OnViewModelPropertyChanged;
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
        await _vm.RefreshVisibleProductAsync();
        await RestartCameraAsync(_cameraCts.Token);
    }

    protected override void OnDisappearing()
    {
        _cameraCts?.Cancel();
        Reader.CameraEnabled = false;
        HideProductActionsImmediately();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
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

        if (_vm.SearchVisible)
        {
            _vm.ToggleSearchCommand.Execute(null);
        }

        return true;
    }

    private void OnDetectionFinished(object? sender, OnDetectionFinishedEventArg e)
    {
        var value = e.BarcodeResults.FirstOrDefault()?.RawValue;
        if (string.IsNullOrEmpty(value)) return;
        MainThread.BeginInvokeOnMainThread(() => _ = _vm.HandleAsync(value));
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
            MainThread.BeginInvokeOnMainThread(async () => await CollapseProductActionsAsync());
    }

    private async Task CollapseProductActionsAsync()
    {
        if (!ProductActionMenu.IsVisible) return;
        await Task.WhenAll(
            ProductActionMenu.FadeToAsync(0, 140),
            ProductActionMenu.TranslateToAsync(0, -12, 140, Easing.CubicIn));
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

    private void OnQuantityEntryUnfocused(object? sender, FocusEventArgs e) =>
        _vm.SetQuantityFromTextCommand.Execute(null);

    private async Task RestartCameraAsync(CancellationToken cancellationToken)
    {
        Reader.CameraEnabled = false;
        try { await Task.Delay(220, cancellationToken); }
        catch (OperationCanceledException) { return; }
        if (!cancellationToken.IsCancellationRequested)
            Reader.CameraEnabled = true;
    }
}
