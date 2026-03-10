using Cartex.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.UI.ViewModels;

public sealed class LocalizationLookup
{
    public string this[string key] => LocalizationManager.Instance[key];
}

public abstract class ViewModelBase : ObservableObject
{
    private LocalizationLookup _l = new();
    private readonly Action _refreshAction;
    public LocalizationLookup L => _l;

    protected ViewModelBase()
    {
        _refreshAction = RefreshLocalization;
        LocalizationManager.Instance.LanguageChanged += _refreshAction;
    }

    private void RefreshLocalization()
    {
        _l = new LocalizationLookup();
        OnPropertyChanged(nameof(L));
    }
}

