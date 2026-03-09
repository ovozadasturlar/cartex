using CommunityToolkit.Mvvm.ComponentModel;
using Cartex.UI.Services;

namespace Cartex.UI.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public LocalizationManager L => LocalizationManager.Instance;
}
