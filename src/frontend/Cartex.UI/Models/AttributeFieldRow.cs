using CommunityToolkit.Mvvm.ComponentModel;

namespace Cartex.UI.Models;

public partial class AttributeFieldRow : ObservableObject
{
    [ObservableProperty] private string _key = "";
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private string _type = "text";
    [ObservableProperty] private bool _required;
    [ObservableProperty] private string _options = "";
    [ObservableProperty] private string _suffix = "";
    [ObservableProperty] private bool _variantDefining;
}
