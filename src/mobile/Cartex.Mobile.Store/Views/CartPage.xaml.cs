using Cartex.Mobile.Store.ViewModels;
using Cartex.Mobile.Store.Services;
using Microsoft.Maui.Controls;

namespace Cartex.Mobile.Store.Views;

public partial class CartPage : ContentPage
{
    private readonly CartViewModel _vm;
    private SwipeView? _openSwipeView;

    public CartPage(CartViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CartViewModel.IsCustomerModalOpen) && !_vm.IsCustomerModalOpen)
                KeyboardDismissal.Hide();
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Appear();
    }

    protected override void OnDisappearing()
    {
        KeyboardDismissal.Hide();
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

        return base.OnBackButtonPressed();
    }

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
        
        if (swipeView.BindingContext is Services.CartLine line)
            line.IsSwiped = true;
    }

    private void OnSwipeEnded(object? sender, SwipeEndedEventArgs e)
    {
        if (sender is not SwipeView swipeView)
            return;

        if (swipeView.BindingContext is Services.CartLine line)
        {
            line.IsSwiped = e.IsOpen;
            if (!e.IsOpen && _openSwipeView == swipeView)
                _openSwipeView = null;
        }
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
}
