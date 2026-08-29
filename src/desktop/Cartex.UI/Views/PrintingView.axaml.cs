using Avalonia.Controls;
using Avalonia.Input;
using Cartex.UI.ViewModels;

namespace Cartex.UI.Views;

public partial class PrintingView : UserControl
{
    public PrintingView() => InitializeComponent();

    private async void OnEndpointDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: NetworkRouteEndpointItem endpoint } || !endpoint.IsSelected)
            return;
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.CreateText(endpoint.EndpointId.ToString()));
        await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Move);
    }

    private void OnEndpointDragOver(object? sender, DragEventArgs e)
    {
        var source = DraggedEndpoint(e);
        var target = (sender as Control)?.DataContext as NetworkRouteEndpointItem;
        e.DragEffects = source?.IsSelected == true && target?.IsSelected == true && !ReferenceEquals(source, target)
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private void OnEndpointDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is PrintingViewModel viewModel)
            viewModel.ReorderNetworkEndpoint(DraggedEndpoint(e), (sender as Control)?.DataContext as NetworkRouteEndpointItem);
    }

    private NetworkRouteEndpointItem? DraggedEndpoint(DragEventArgs e)
    {
        if (DataContext is not PrintingViewModel viewModel) return null;
        var text = e.DataTransfer.Items.Select(x => x.TryGetText()).FirstOrDefault(x => long.TryParse(x, out _));
        return long.TryParse(text, out var id)
            ? viewModel.NetworkEndpoints.FirstOrDefault(x => x.EndpointId == id)
            : null;
    }
}
