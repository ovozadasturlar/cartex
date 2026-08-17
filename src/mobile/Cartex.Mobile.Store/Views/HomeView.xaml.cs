using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class HomeView : ContentView, ISectionView
{
    private readonly HomeViewModel _vm;

    public HomeView(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    public void Appear() => _ = _vm.AppearAsync();

    public void Disappear()
    {
    }
}
