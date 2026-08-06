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

        var excelItem = new MenuItem { Header = "Excel", CommandParameter = "Excel" };
        excelItem.Bind(MenuItem.CommandProperty, this.GetObservable(CommandProperty));
        flyout.Items.Add(excelItem);

        var pdfItem = new MenuItem { Header = "PDF" };
        
        var pdfLandscapeItem = new MenuItem { Header = "Horizontal", CommandParameter = "Pdf" };
        pdfLandscapeItem.Bind(MenuItem.CommandProperty, this.GetObservable(CommandProperty));
        pdfItem.Items.Add(pdfLandscapeItem);

        var pdfPortraitItem = new MenuItem { Header = "Vertical", CommandParameter = "PdfPortrait" };
        pdfPortraitItem.Bind(MenuItem.CommandProperty, this.GetObservable(CommandProperty));
        pdfItem.Items.Add(pdfPortraitItem);

        flyout.Items.Add(pdfItem);

        var csvItem = new MenuItem { Header = "CSV", CommandParameter = "Csv" };
        csvItem.Bind(MenuItem.CommandProperty, this.GetObservable(CommandProperty));
        flyout.Items.Add(csvItem);

        Flyout = flyout;
    }

    protected override void OnClick() => Flyout?.ShowAt(this);
}
