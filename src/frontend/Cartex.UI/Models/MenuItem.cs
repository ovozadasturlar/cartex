using CommunityToolkit.Mvvm.ComponentModel;
using Material.Icons;

namespace Cartex.UI.Models;

public partial class MenuItem : ObservableObject
{
    public string Key { get; init; } = null!;
    public MaterialIconKind Icon { get; init; }
    public string? Permission { get; init; }
    public Type ViewModelType { get; init; } = null!;

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private bool _isActive;
}
