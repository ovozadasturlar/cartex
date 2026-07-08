using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Cartex.UI.Controls;

public class ExportButton : TemplatedControl
{
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<ExportButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ExportButton, string?>(nameof(Text));

    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
}
