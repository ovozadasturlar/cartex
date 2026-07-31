using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Cartex.UI.Controls;

public partial class TemplateVariableEditor : UserControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<TemplateVariableEditor, string?>(nameof(Label));
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TemplateVariableEditor, string?>(
            nameof(Text), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<TemplateVariableEditor, string?>(nameof(PlaceholderText));
    public static readonly StyledProperty<string?> DragHintProperty =
        AvaloniaProperty.Register<TemplateVariableEditor, string?>(nameof(DragHint));

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public string? DragHint
    {
        get => GetValue(DragHintProperty);
        set => SetValue(DragHintProperty, value);
    }

    public IReadOnlyList<string> Variables { get; } =
        ["{name}", "{balance}", "{currency}", "{days}", "{dueDate}"];

    public TemplateVariableEditor() => InitializeComponent();

    private void OnVariableClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string token })
            Insert(token);
    }

    private async void OnDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { Tag: string token })
            return;

        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.CreateText(token));
        await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Copy);
    }

    private void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Items.Any(x => x.Contains(DataFormat.Text))
            ? DragDropEffects.Copy
            : DragDropEffects.None;

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var token = e.DataTransfer.Items.Select(x => x.TryGetText()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (token is not null)
            Insert(token);
    }

    private void Insert(string token)
    {
        var current = Editor.Text ?? string.Empty;
        var start = Math.Clamp(Editor.SelectionStart, 0, current.Length);
        var end = Math.Clamp(Editor.SelectionEnd, start, current.Length);
        Editor.Text = current[..start] + token + current[end..];
        Editor.CaretIndex = start + token.Length;
        Editor.SelectionStart = Editor.CaretIndex;
        Editor.SelectionEnd = Editor.CaretIndex;
        Editor.Focus();
    }
}
