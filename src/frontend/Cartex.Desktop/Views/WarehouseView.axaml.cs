using Avalonia.Controls;
using Cartex.Desktop.ViewModels;

namespace Cartex.Desktop.Views;

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
