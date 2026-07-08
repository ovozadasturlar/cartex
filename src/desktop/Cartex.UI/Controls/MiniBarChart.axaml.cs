using System.Collections;
using Avalonia;
using Avalonia.Controls;

namespace Cartex.UI.Controls;

public partial class MiniBarChart : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ColumnsProperty =
        AvaloniaProperty.Register<MiniBarChart, IEnumerable?>(nameof(Columns));

    public IEnumerable? Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    public MiniBarChart() => InitializeComponent();
}
