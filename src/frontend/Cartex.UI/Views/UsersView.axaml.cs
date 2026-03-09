using Avalonia.Controls;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

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
