using System.ComponentModel;
using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class CustomersView : ContentView, ISectionView
{
    private readonly CustomersViewModel _vm;

    public CustomersView(CustomersViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    public void Appear() => _ = _vm.AppearAsync();

    public void Disappear()
    {
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CustomersViewModel.IsCreateModalOpen) && MainPage.Current is { } page)
            page.BarVisible = !_vm.IsCreateModalOpen;
    }
}
