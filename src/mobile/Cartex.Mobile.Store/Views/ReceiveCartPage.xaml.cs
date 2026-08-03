using Cartex.Mobile.Store.ViewModels;
using Cartex.Mobile.Store.Services;

namespace Cartex.Mobile.Store.Views;

public partial class ReceiveCartPage : ContentPage
{
    private readonly ReceiveCartViewModel _vm;
    private SwipeView? _openSwipeView;

    public ReceiveCartPage(ReceiveCartViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Appear();
    }

    protected override void OnDisappearing()
    {
        KeyboardDismissal.Hide();
        base.OnDisappearing();
        _vm.Disappear();
    }

    private void OnSwipeStarted(object? sender, SwipeStartedEventArgs e)
    {
        if (sender is not SwipeView swipeView) return;
        if (_openSwipeView is not null && _openSwipeView != swipeView)
        {
            _openSwipeView.Close();
            if (_openSwipeView.BindingContext is Services.SupplyCartLine previous)
                previous.IsSwiped = false;
        }
        _openSwipeView = swipeView;
        if (swipeView.BindingContext is Services.SupplyCartLine line)
            line.IsSwiped = true;
    }

    private void OnSwipeEnded(object? sender, SwipeEndedEventArgs e)
    {
        if (sender is not SwipeView swipeView || swipeView.BindingContext is not Services.SupplyCartLine line) return;
        line.IsSwiped = e.IsOpen;
        if (!e.IsOpen && _openSwipeView == swipeView)
            _openSwipeView = null;
    }
}
