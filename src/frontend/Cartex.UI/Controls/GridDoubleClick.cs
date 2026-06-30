using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Cartex.UI.Controls;

public static class GridDoubleClick
{
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<DataGrid, ICommand?>("Command", typeof(GridDoubleClick));

    public static ICommand? GetCommand(DataGrid grid) => grid.GetValue(CommandProperty);
    public static void SetCommand(DataGrid grid, ICommand? value) => grid.SetValue(CommandProperty, value);

    static GridDoubleClick()
    {
        CommandProperty.Changed.AddClassHandler<DataGrid>((grid, args) =>
        {
            grid.DoubleTapped -= OnDoubleTapped;
            if (args.NewValue is not null)
                grid.DoubleTapped += OnDoubleTapped;
        });
    }

    private static void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        var command = GetCommand(grid);
        var item = grid.SelectedItem;
        if (command is not null && item is not null && command.CanExecute(item))
            command.Execute(item);
    }
}
