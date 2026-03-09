using Avalonia.Controls;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class WarehouseView : UserControl
{
    public WarehouseView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is WarehouseViewModel vm)
                await vm.LoadWarehousesCommand.ExecuteAsync(null);
        };
    }
}
