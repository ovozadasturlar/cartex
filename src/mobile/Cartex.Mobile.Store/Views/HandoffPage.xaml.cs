using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class HandoffPage : ContentPage
{
    private readonly HandoffViewModel _vm;
    private CancellationTokenSource? _cts;

    public HandoffPage(HandoffViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _cts = new CancellationTokenSource();
        _ = _vm.PollAsync(_cts.Token);
    }

    protected override void OnDisappearing()
    {
        _cts?.Cancel();
        _cts = null;
        base.OnDisappearing();
    }
}
