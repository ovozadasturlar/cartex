using Avalonia.Controls;
using Cartex.Desktop.ViewModels;

namespace Cartex.Desktop.Views;

public partial class RolesView : UserControl
{
    public RolesView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is RolesViewModel vm)
                await vm.LoadCommand.ExecuteAsync(null);
        };
    }
}
