using Cartex.Mobile.Store.ViewModels;

namespace Cartex.Mobile.Store.Views;

public partial class ProfileView : ContentView, ISectionView
{
    private readonly ProfileViewModel _vm;

    public ProfileView(ProfileViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    public void Appear() => _ = _vm.AppearAsync();

    public void Disappear()
    {
    }
}
