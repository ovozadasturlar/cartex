using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.UI.Models;

public partial class MenuSection : ObservableObject
{
    public string Key { get; init; } = null!;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private bool _isVisible = true;
    public ObservableCollection<MenuItem> Items { get; } = [];
}
