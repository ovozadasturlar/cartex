using Avalonia.Controls;
using Cartex.Desktop.ViewModels;

namespace Cartex.Desktop.Views;

public partial class UsersView : UserControl
{
    public UsersView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is UsersViewModel vm)
                await vm.LoadCommand.ExecuteAsync(null);
        };
    }
}
