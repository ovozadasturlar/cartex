using System.ComponentModel;
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
    public LocalizationLookup L => _l;

    protected ViewModelBase()
    {
        LocalizationManager.Instance.PropertyChanged += OnLocalizationChanged;
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Item[]")
        {
            _l = new LocalizationLookup();
            OnPropertyChanged(nameof(L));
        }
    }
}

