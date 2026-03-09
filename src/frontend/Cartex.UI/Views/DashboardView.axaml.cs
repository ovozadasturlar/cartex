using Avalonia.Controls;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();

        Loaded += async (_, _) =>
        {
            if (DataContext is DashboardViewModel vm)
                await vm.LoadCommand.ExecuteAsync(null);
        };
    }
}
