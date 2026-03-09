using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.UI.Models;

public partial class MenuItem : ObservableObject
{
    public string Key { get; init; } = null!;
    public string Icon { get; init; } = null!;
    public string? Permission { get; init; }
    public Type ViewModelType { get; init; } = null!;

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private bool _isActive;
}
