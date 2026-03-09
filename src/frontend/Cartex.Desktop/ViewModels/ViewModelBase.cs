using CommunityToolkit.Mvvm.ComponentModel;
using Cartex.Desktop.Services;

namespace Cartex.Desktop.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public LocalizationManager L => LocalizationManager.Instance;
}
