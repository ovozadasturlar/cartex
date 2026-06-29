using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.UI.Models;

public partial class ProductAttributeVM : ObservableObject
{
    public required string Key { get; init; }
    public required string Display { get; init; }
    public required string Type { get; init; }
    public bool Required { get; init; }
    public string? Suffix { get; init; }
    public ObservableCollection<string> Options { get; } = [];

    [ObservableProperty] private string _textValue = "";
    [ObservableProperty] private decimal? _numberValue;
    [ObservableProperty] private bool _boolValue;
    [ObservableProperty] private string? _selectValue;

    public bool IsText => Type == "text";
    public bool IsNumber => Type == "number";
    public bool IsBool => Type == "bool";
    public bool IsSelect => Type == "select";
}
