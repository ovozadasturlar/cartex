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
        _vm.RowInteracted = CloseOpenSwipe;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Appear();
    }

    protected override void OnDisappearing()
    {
        KeyboardDismissal.Hide();
        CloseOpenSwipe();
        base.OnDisappearing();
        _vm.Disappear();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_vm.IsLineModalOpen)
        {
            _vm.CloseLineModalCommand.Execute(null);
            return true;
        }

        if (_vm.IsSupplierModalOpen)
        {
            _vm.CloseSupplierModalCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    /// The totals hide behind the stepper as the row opens, so they follow the real drag
    /// distance. SwipeEnded reports the row as open even after a swipe back, which used to
    /// leave the totals hidden until the row was touched again.
    private const double RevealedOffset = 4;

    private void OnSwipeStarted(object? sender, SwipeStartedEventArgs e)
    {
        if (sender is not SwipeView swipeView) return;
        if (_openSwipeView is not null && _openSwipeView != swipeView)
        {
            _openSwipeView.Close();
            if (_openSwipeView.BindingContext is SupplyCartLine previous)
                previous.IsSwiped = false;
        }
        _openSwipeView = swipeView;
    }

    private void OnSwipeChanging(object? sender, SwipeChangingEventArgs e)
    {
        if (sender is SwipeView { BindingContext: SupplyCartLine line })
            line.IsSwiped = Math.Abs(e.Offset) > RevealedOffset;
    }

    private void OnSwipeEnded(object? sender, SwipeEndedEventArgs e)
    {
        if (sender is not SwipeView swipeView) return;
        if (swipeView.BindingContext is SupplyCartLine { IsSwiped: false } && _openSwipeView == swipeView)
            _openSwipeView = null;
    }

    private void OnOutsideTapped(object? sender, TappedEventArgs e) => CloseOpenSwipe();

    private void OnListScrolled(object? sender, ItemsViewScrolledEventArgs e) => CloseOpenSwipe();

    private bool CloseOpenSwipe()
    {
        if (_openSwipeView is null) return false;
        _openSwipeView.Close();
        if (_openSwipeView.BindingContext is SupplyCartLine line)
            line.IsSwiped = false;
        _openSwipeView = null;
        return true;
    }

    private void OnQtyEntryCompleted(object? sender, EventArgs e)
    {
        _vm.SetSelectedQuantityFromTextCommand.Execute(null);
        KeyboardDismissal.Hide();
    }

    private void OnQtyEntryUnfocused(object? sender, FocusEventArgs e)
    {
        _vm.SetSelectedQuantityFromTextCommand.Execute(null);
    }
}
