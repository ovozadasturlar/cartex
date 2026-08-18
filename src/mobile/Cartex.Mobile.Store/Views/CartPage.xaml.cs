using Cartex.Mobile.Store.ViewModels;
using Cartex.Mobile.Store.Services;

namespace Cartex.Mobile.Store.Views;

public partial class CartPage : ContentPage
{
    private readonly CartViewModel _vm;
    private SwipeView? _openSwipeView;

    public CartPage(CartViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.RowInteracted = CloseOpenSwipe;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CartViewModel.IsCustomerModalOpen) && !_vm.IsCustomerModalOpen)
                KeyboardDismissal.Hide();
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AppearAsync();
    }

    protected override void OnDisappearing()
    {
        KeyboardDismissal.Hide();
        CloseOpenSwipe();
        _vm.Disappear();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_vm.IsProductModalOpen)
        {
            _vm.CloseProductModalCommand.Execute(null);
            return true;
        }

        if (_vm.IsCustomerModalOpen)
        {
            _vm.CloseCustomerModalCommand.Execute(null);
            return true;
        }

        if (_vm.IsParticipantModalOpen)
        {
            _vm.CloseParticipantModalCommand.Execute(null);
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
        if (sender is not SwipeView swipeView)
            return;

        if (_openSwipeView != null && _openSwipeView != swipeView)
        {
            _openSwipeView.Close();
            if (_openSwipeView.BindingContext is Services.CartLine oldLine)
                oldLine.IsSwiped = false;
        }
        _openSwipeView = swipeView;
    }

    private void OnSwipeChanging(object? sender, SwipeChangingEventArgs e)
    {
        if (sender is SwipeView { BindingContext: Services.CartLine line })
            line.IsSwiped = Math.Abs(e.Offset) > RevealedOffset;
    }

    private void OnSwipeEnded(object? sender, SwipeEndedEventArgs e)
    {
        if (sender is not SwipeView swipeView)
            return;

        if (swipeView.BindingContext is Services.CartLine { IsSwiped: false } && _openSwipeView == swipeView)
            _openSwipeView = null;
    }

    private void OnQuantityEntryCompleted(object? sender, EventArgs e)
    {
        _vm.SetQuantityFromTextCommand.Execute(null);
        KeyboardDismissal.Hide();
    }

    private void OnQuantityEntryUnfocused(object? sender, FocusEventArgs e)
    {
        _vm.SetQuantityFromTextCommand.Execute(null);
    }

    private void OnPriceEntryCompleted(object? sender, EventArgs e)
    {
        _vm.SetPriceFromTextCommand.Execute(null);
        KeyboardDismissal.Hide();
    }

    private void OnPriceEntryUnfocused(object? sender, FocusEventArgs e)
    {
        _vm.SetPriceFromTextCommand.Execute(null);
    }

    private void OnOutsideTapped(object? sender, TappedEventArgs e) => CloseOpenSwipe();

    private void OnCartScrolled(object? sender, ItemsViewScrolledEventArgs e) => CloseOpenSwipe();

    private bool CloseOpenSwipe()
    {
        if (_openSwipeView is null)
            return false;

        _openSwipeView.Close();
        if (_openSwipeView.BindingContext is Services.CartLine line)
            line.IsSwiped = false;
        _openSwipeView = null;
        return true;
    }
}
