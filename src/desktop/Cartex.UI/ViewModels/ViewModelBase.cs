using System.Windows.Input;
using Avalonia.Input;
using Cartex.UI.Models;
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

    protected static IReadOnlyList<PageShortcut> CrudShortcuts(ICommand openCreate, ICommand save, Action close, Func<bool> isEditOpen) =>
    [
        new(Key.N, KeyModifiers.Control, "shortcut_new", () => openCreate.Execute(null), () => !isEditOpen(), WorksInText: true),
        new(Key.F2, KeyModifiers.None, "shortcut_save", () => save.Execute(null), isEditOpen, WorksInText: true),
        new(Key.Escape, KeyModifiers.None, "shortcut_close", close, isEditOpen, WorksInText: true),
    ];
}

