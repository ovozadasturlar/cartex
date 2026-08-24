using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class CategoriesView : UserControl
{
    public CategoriesView() => InitializeComponent();

    private async void OnDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { Tag: CategoryTreeItem item } control
            || DataContext is not CategoriesViewModel { CanEdit: true }
            || !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
            return;

        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.CreateText(item.Id.ToString()));
        await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Move);
        if (DataContext is CategoriesViewModel viewModel)
            viewModel.ClearDrop();
    }

    private void OnRowDragOver(object? sender, DragEventArgs e)
    {
        if (!TryGetDrop(sender, e, out var viewModel, out var sourceId, out var target, out var relativeY))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = viewModel.PreviewDrop(sourceId, target.Id, relativeY)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        AutoScroll(e);
    }

    private void OnRowDragLeave(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CategoriesViewModel viewModel)
            viewModel.ClearDrop();
    }

    private async void OnRowDrop(object? sender, DragEventArgs e)
    {
        if (TryGetDrop(sender, e, out var viewModel, out var sourceId, out var target, out var relativeY))
            await viewModel.DropAsync(sourceId, target.Id, relativeY);
    }

    private async void OnRowKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Alt)
            || sender is not Control { DataContext: CategoryTreeItem item }
            || DataContext is not CategoriesViewModel viewModel)
            return;

        var move = e.Key switch
        {
            Key.Up => CategoryKeyboardMove.Up,
            Key.Down => CategoryKeyboardMove.Down,
            Key.Left => CategoryKeyboardMove.Outdent,
            Key.Right => CategoryKeyboardMove.Indent,
            _ => (CategoryKeyboardMove?)null
        };
        if (move is null) return;
        e.Handled = true;
        await viewModel.MoveByKeyboardAsync(item, move.Value);
    }

    private bool TryGetDrop(
        object? sender,
        DragEventArgs e,
        out CategoriesViewModel viewModel,
        out long sourceId,
        out CategoryTreeItem target,
        out double relativeY)
    {
        viewModel = DataContext as CategoriesViewModel ?? null!;
        target = (sender as Control)?.DataContext as CategoryTreeItem ?? null!;
        sourceId = 0;
        relativeY = 0.5;
        if (viewModel is null || target is null || sender is not Control row)
            return false;

        var text = e.DataTransfer.Items.Select(x => x.TryGetText()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (!long.TryParse(text, out sourceId)) return false;
        relativeY = row.Bounds.Height <= 0 ? 0.5 : e.GetPosition(row).Y / row.Bounds.Height;
        return true;
    }

    private void AutoScroll(DragEventArgs e)
    {
        var position = e.GetPosition(TreeScroll);
        var delta = position.Y < 36 ? -24 : position.Y > TreeScroll.Bounds.Height - 36 ? 24 : 0;
        if (delta == 0) return;
        var maximum = Math.Max(0, TreeScroll.Extent.Height - TreeScroll.Viewport.Height);
        TreeScroll.Offset = new Vector(TreeScroll.Offset.X, Math.Clamp(TreeScroll.Offset.Y + delta, 0, maximum));
    }
}
