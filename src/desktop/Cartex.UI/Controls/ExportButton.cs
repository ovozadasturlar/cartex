using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Material.Icons;
using Material.Icons.Avalonia;

namespace Cartex.UI.Controls;

public class ExportButton : Button
{
    protected override Type StyleKeyOverride => typeof(Button);

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ExportButton, string?>(nameof(Text));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public ExportButton()
    {
        Padding = new Thickness(12, 8);
        Cursor = new Cursor(StandardCursorType.Hand);

        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        label.Bind(TextBlock.TextProperty, this.GetObservable(TextProperty));
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new MaterialIcon { Kind = MaterialIconKind.DownloadOutline, Width = 16, Height = 16 },
                label
            }
        };

        var flyout = new MenuFlyout();
        foreach (var (header, param) in new[] { ("Excel", "Excel"), ("PDF", "Pdf"), ("CSV", "Csv") })
        {
            var item = new MenuItem { Header = header, CommandParameter = param };
            item.Bind(MenuItem.CommandProperty, this.GetObservable(CommandProperty));
            flyout.Items.Add(item);
        }
        Flyout = flyout;
    }

    protected override void OnClick() => Flyout?.ShowAt(this);
}
