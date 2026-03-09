using CommunityToolkit.Mvvm.ComponentModel;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Services;

public sealed partial class NavigationService : ObservableObject
{
    [ObservableProperty]
    private ViewModelBase? _currentView;

    public event Action<string>? MenuNavigationRequested;

    public void NavigateTo(ViewModelBase viewModel) => CurrentView = viewModel;

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        var vm = ServiceLocator.Resolve<TViewModel>();
        CurrentView = vm;
    }

    public void RequestMenuNavigation(string menuKey) =>
        MenuNavigationRequested?.Invoke(menuKey);
}
